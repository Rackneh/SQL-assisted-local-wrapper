"""
Download script for NVIDIA-Nemotron-Labs-Dory1.0-4B-GGUF.
Downloads the official Q5_K_M GGUF model into the models/ directory and verifies SHA-256.
"""

import hashlib
import os
import sys
from huggingface_hub import hf_hub_download

REPO_ID = "nvidia/NVIDIA-Nemotron-Labs-Dory1.0-4B-GGUF"
FILENAME = "NVIDIA-Nemotron-Labs-Dory1.0-4B-Q5_K_M.gguf"
EXPECTED_SHA256 = "96080be49ffd67b916018b7bdef8c484762576fb52780e10ab98ac4978a660e0"
TARGET_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "models")
TARGET_FILE = os.path.join(TARGET_DIR, FILENAME)


def verify_sha256(filepath: str, expected_hash: str) -> bool:
    print(f"Verifying SHA-256 for {filepath}...")
    sha256 = hashlib.sha256()
    total_size = os.path.getsize(filepath)
    read_so_far = 0
    with open(filepath, "rb") as f:
        while chunk := f.read(1024 * 1024 * 8):  # 8MB chunks
            sha256.update(chunk)
            read_so_far += len(chunk)
            pct = (read_so_far / total_size) * 100
            print(f"\rVerification progress: {pct:.1f}%", end="", flush=True)
    print()
    computed_hash = sha256.hexdigest().lower()
    print(f"Expected: {expected_hash.lower()}")
    print(f"Computed: {computed_hash}")
    return computed_hash == expected_hash.lower()


def main():
    os.makedirs(TARGET_DIR, exist_ok=True)

    if os.path.exists(TARGET_FILE):
        print(f"Model file already exists at: {TARGET_FILE}")
        if verify_sha256(TARGET_FILE, EXPECTED_SHA256):
            print("Model SHA-256 checksum matches! Ready to use.")
            return
        else:
            print("Checksum mismatch! Re-downloading...")

    print(f"Downloading {FILENAME} from {REPO_ID}...")
    downloaded_path = hf_hub_download(
        repo_id=REPO_ID,
        filename=FILENAME,
        local_dir=TARGET_DIR,
        local_dir_use_symlinks=False,
    )
    print(f"Downloaded to {downloaded_path}")

    if verify_sha256(TARGET_FILE, EXPECTED_SHA256):
        print("Verification successful!")
    else:
        print("WARNING: Checksum did not match expected value!")
        sys.exit(1)


if __name__ == "__main__":
    main()
