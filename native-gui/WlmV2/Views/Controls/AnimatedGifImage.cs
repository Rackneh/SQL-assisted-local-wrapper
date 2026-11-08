using System;
using System.Collections.Generic;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PixelFormats = System.Windows.Media.PixelFormats;

namespace WLMRevivalV2Desktop.Views.Controls
{
    /// <summary>
    /// A WPF Image that downloads and plays an animated GIF from a URL. .NET
    /// Framework's WPF has no built-in GIF animation, so this decodes every frame
    /// (honouring per-frame offset, delay and disposal) into composited snapshots
    /// up front, then cycles them with a DispatcherTimer. Falls back to a static
    /// image if decoding fails. Bytes are cached in-process so the picker and the
    /// chat transcript don't re-download the same GIF.
    /// </summary>
    public class AnimatedGifImage : Image
    {
        private static readonly Dictionary<string, byte[]> _byteCache =
            new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _cacheLock = new object();

        private DispatcherTimer _timer;
        private List<BitmapSource> _frames;
        private List<int> _delaysMs;
        private int _index;
        private string _currentUrl;

        public AnimatedGifImage()
        {
            Unloaded += (s, e) => StopAnimation();
        }

        public void BeginLoad(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            _currentUrl = url;
            StopAnimation();

            byte[] cached;
            lock (_cacheLock) _byteCache.TryGetValue(url, out cached);
            if (cached != null)
            {
                DecodeAndPlay(cached, url);
                return;
            }

            System.Threading.Tasks.Task.Run(() =>
            {
                byte[] data = null;
                try
                {
                    using (var client = new WebClient())
                        data = client.DownloadData(url);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("AnimatedGifImage download: " + ex.Message);
                }

                if (data == null) return;
                lock (_cacheLock) _byteCache[url] = data;

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // Ignore if the control was reused for a different URL meanwhile.
                    if (_currentUrl == url) DecodeAndPlay(data, url);
                }));
            });
        }

        private void DecodeAndPlay(byte[] data, string url)
        {
            try
            {
                using (var ms = new System.IO.MemoryStream(data))
                {
                    var decoder = new GifBitmapDecoder(ms,
                        BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);

                    int frameCount = decoder.Frames.Count;
                    if (frameCount == 0) return;

                    if (frameCount == 1)
                    {
                        var single = decoder.Frames[0];
                        single.Freeze();
                        Source = single;
                        return;
                    }

                    BuildFrames(decoder);
                    if (_frames == null || _frames.Count == 0) return;

                    _index = 0;
                    Source = _frames[0];
                    _timer = new DispatcherTimer();
                    _timer.Tick += OnTick;
                    _timer.Interval = TimeSpan.FromMilliseconds(_delaysMs[0]);
                    _timer.Start();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("AnimatedGifImage decode: " + ex.Message);
                // Fall back to a static render of the first frame.
                try
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.StreamSource = new System.IO.MemoryStream(data);
                    bi.EndInit();
                    bi.Freeze();
                    Source = bi;
                }
                catch { /* give up */ }
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (_frames == null || _frames.Count == 0) return;
            _index = (_index + 1) % _frames.Count;
            Source = _frames[_index];
            _timer.Interval = TimeSpan.FromMilliseconds(_delaysMs[_index]);
        }

        private void StopAnimation()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= OnTick;
                _timer = null;
            }
            _frames = null;
            _delaysMs = null;
            _index = 0;
        }

        // ---- GIF frame compositing (offset / delay / disposal) ----
        private void BuildFrames(GifBitmapDecoder decoder)
        {
            int count = decoder.Frames.Count;

            // Logical canvas size = the extent that covers every frame rectangle.
            int w = 0, h = 0;
            for (int i = 0; i < count; i++)
            {
                var f = decoder.Frames[i];
                int left = MetaInt(f, "/imgdesc/Left", 0);
                int top = MetaInt(f, "/imgdesc/Top", 0);
                w = Math.Max(w, left + f.PixelWidth);
                h = Math.Max(h, top + f.PixelHeight);
            }
            if (w <= 0 || h <= 0) return;

            int stride = w * 4;
            byte[] canvas = new byte[stride * h];      // transparent
            byte[] backup = null;
            int prevDisposal = 0;
            Int32Rect prevRect = new Int32Rect(0, 0, 0, 0);

            _frames = new List<BitmapSource>(count);
            _delaysMs = new List<int>(count);

            for (int i = 0; i < count; i++)
            {
                var frame = decoder.Frames[i];
                int left = MetaInt(frame, "/imgdesc/Left", 0);
                int top = MetaInt(frame, "/imgdesc/Top", 0);
                int delay = MetaInt(frame, "/grctlext/Delay", 0) * 10; // centi -> ms
                if (delay < 20) delay = 100;                            // sane default
                int disposal = MetaInt(frame, "/grctlext/Disposal", 0);

                // Apply the *previous* frame's disposal before drawing this one.
                if (i > 0)
                {
                    if (prevDisposal == 2)
                        ClearRect(canvas, stride, w, h, prevRect);
                    else if (prevDisposal == 3 && backup != null)
                        Array.Copy(backup, canvas, canvas.Length);
                }

                // If this frame wants "restore to previous", snapshot before drawing.
                if (disposal == 3)
                    backup = (byte[])canvas.Clone();

                DrawFrame(canvas, stride, w, h, frame, left, top);

                var snap = BitmapSource.Create(w, h, 96, 96,
                    PixelFormats.Bgra32, null, (byte[])canvas.Clone(), stride);
                snap.Freeze();
                _frames.Add(snap);
                _delaysMs.Add(delay);

                prevDisposal = disposal;
                prevRect = new Int32Rect(left, top, frame.PixelWidth, frame.PixelHeight);
            }
        }

        private static void DrawFrame(byte[] canvas, int stride, int w, int h,
            BitmapFrame frame, int left, int top)
        {
            var conv = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            int fw = conv.PixelWidth, fh = conv.PixelHeight;
            int fstride = fw * 4;
            byte[] fb = new byte[fstride * fh];
            conv.CopyPixels(fb, fstride, 0);

            for (int y = 0; y < fh; y++)
            {
                int cy = top + y;
                if (cy < 0 || cy >= h) continue;
                for (int x = 0; x < fw; x++)
                {
                    int si = y * fstride + x * 4;
                    byte a = fb[si + 3];
                    if (a == 0) continue; // GIF transparency: leave canvas pixel
                    int cx = left + x;
                    if (cx < 0 || cx >= w) continue;
                    int di = cy * stride + cx * 4;
                    canvas[di] = fb[si];
                    canvas[di + 1] = fb[si + 1];
                    canvas[di + 2] = fb[si + 2];
                    canvas[di + 3] = 255;
                }
            }
        }

        private static void ClearRect(byte[] canvas, int stride, int w, int h, Int32Rect r)
        {
            for (int y = r.Y; y < r.Y + r.Height; y++)
            {
                if (y < 0 || y >= h) continue;
                for (int x = r.X; x < r.X + r.Width; x++)
                {
                    if (x < 0 || x >= w) continue;
                    int di = y * stride + x * 4;
                    canvas[di] = 0; canvas[di + 1] = 0; canvas[di + 2] = 0; canvas[di + 3] = 0;
                }
            }
        }

        private static int MetaInt(BitmapFrame frame, string query, int def)
        {
            try
            {
                var md = frame.Metadata as BitmapMetadata;
                if (md != null && md.ContainsQuery(query))
                {
                    object o = md.GetQuery(query);
                    if (o != null) return Convert.ToInt32(o);
                }
            }
            catch { /* metadata absent */ }
            return def;
        }
    }
}
