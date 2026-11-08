"""Counted response schema shared by the host and LM Studio grammar decoder."""
import json
from .tools import TOOL_HELP


STRING = {'type':'string'}
INTEGER = {'type':'integer'}
TOOL_ARGUMENTS = {
    'search_evidence': ({'query':STRING,'database':STRING,'limit':INTEGER}, ['query']),
    'read_evidence': ({'id':INTEGER}, ['id']),
    'read_document': ({'id':STRING,'after':INTEGER,'limit':INTEGER}, ['id']),
    'read_artifact': ({'id':STRING,'offset':INTEGER,'limit':INTEGER}, ['id']),
    'databases': ({}, []),
    'create_database': ({'name':STRING,'kind':{'enum':['knowledge','math','code','higher','lower']}}, ['name','kind']),
    'remember': ({'database':STRING,'source':STRING,'text':STRING,'evidence':{'$ref':'#/$defs/evidence'}}, ['database','source','text','evidence']),
    'read_file': ({'path':STRING,'offset':INTEGER,'limit':INTEGER,'line':INTEGER}, ['path']),
    'find_files': ({'pattern':STRING,'limit':INTEGER}, ['pattern']),
    'search_text': ({'pattern':STRING,'path':STRING,'glob':STRING,'limit':INTEGER}, ['pattern']),
    'list_directory': ({'path':STRING,'after':STRING,'limit':INTEGER}, []),
    'ingest_file': ({'path':STRING,'database':STRING}, ['path']),
    'write_file': ({'path':STRING,'text':STRING,'expected_hash':{'type':'string','pattern':'^(absent|[a-f0-9]{64})$'}}, ['path','text','expected_hash']),
    'edit_file': ({'path':STRING,'old_text':STRING,'new_text':STRING,'expected_hash':{'type':'string','pattern':'^(absent|[a-f0-9]{64})$'},'replace_all':{'type':'boolean'}}, ['path','old_text','new_text','expected_hash']),
    'calculate': ({'expression':STRING}, ['expression']),
    'list_checks': ({}, []),
    'run_check': ({'name':STRING}, ['name']),
    'scope_document': ({'id':STRING}, ['id']),
    'record_coverage': ({'id':INTEGER,'finding':STRING}, ['id','finding']),
    'coverage_status': ({}, []),
    'query_code_map': ({'path':STRING,'query':STRING}, []),
    'propose_improvement': ({'title':STRING,'observation':STRING,'proposal':STRING,'category':STRING,'expected_impact':STRING}, ['title','observation','proposal']),
    'switch_backend': ({'backend':{'enum':['dory','lmstudio']},'reason':STRING}, ['backend']),
}
assert set(TOOL_ARGUMENTS) == set(TOOL_HELP)


def object_schema(properties, required=None):
    return {'type':'object','properties':properties,'required':list(properties) if required is None else required,'additionalProperties':False}


def visible_evidence(state):
    ids = {e['id'] for e in state.get('evidence', []) + state.get('constraints', []) if isinstance(e, dict) and 'id' in e}
    for receipt in state.get('recent_actions', []) + state.get('ancestor_observations', []):
        artifact = receipt.get('result')
        if not isinstance(artifact, dict) or artifact.get('next_offset') is not None:
            continue
        try:
            value = artifact['value'] if 'value' in artifact else json.loads(artifact.get('body', '{}'))
            if receipt.get('tool') == 'read_evidence': ids.add(value['id'])
            if receipt.get('tool') == 'read_document': ids.update(e['id'] for e in value.get('chunks', []))
            if receipt.get('tool') == 'search_evidence': ids.update(e['id'] for e in value)
        except (ValueError, KeyError, TypeError):
            pass
    return ids


def compact_outcome_contract():
    return """Exact Outcome JSON Formats (return exactly one JSON object without fences):
- Action: {"kind":"action","text":"concise summary","tool":"<name>","tool_arguments":{<args>},"children":[],"evidence":[],"coverage":[]}
  (tool_arguments may be a list of 1–4 objects for batched read-only calls of the same tool)
- Decompose: {"kind":"decompose","text":"plan","children":[{"objective":"...","requirements":["..."],"allow_decompose":false}],"tool":"","tool_arguments":{},"evidence":[],"coverage":[]}
- Finish: {"kind":"finish","text":"final outcome summary","coverage":[],"tool":"","tool_arguments":{},"children":[],"evidence":[]}
- Wait: {"kind":"wait","text":"question for user","options":["choice A","choice B"],"tool":"","tool_arguments":{},"children":[],"evidence":[],"coverage":[]}
  (options is optional: 1–6 short clickable choices; omit it for open-ended questions)"""


def outcome_schema(state):
    """Use the same state-derived contract for token counting and decoding."""
    kinds = state.get('allowed_kinds', ['action','decompose','finish','wait'])
    if any(c['status'] != 'passed' for c in state.get('completion_checks', [])):
        kinds = [kind for kind in kinds if kind != 'finish']
    ids = sorted(visible_evidence(state))
    evidence = {'type':'array','items':{'type':'integer','enum':ids}} if ids else {'type':'array','items':INTEGER,'maxItems':0}
    ref = {'$ref':'#/$defs/evidence'}
    requirements = state.get('requirements', [])
    coverage = {'type':'array','items':object_schema({'requirement':{'type':'integer','enum':list(range(len(requirements)))},'evidence':ref,'explanation':STRING})} if requirements else {'type':'array','items':{},'maxItems':0}
    branches = []
    for kind in kinds:
        # Every branch has the same keys. Grammar backends may sort object keys;
        # different first keys must not accidentally select wait over a tool.
        common = {'kind':{'enum':[kind]},'text':STRING,'evidence':ref,
                  'tool':{'enum':['']},'tool_arguments':{'$ref':'#/$defs/emptyObject'},
                  'children':{'$ref':'#/$defs/emptyArray'},'coverage':{'$ref':'#/$defs/emptyArray'},
                  'options':{'type':'array','items':STRING,'maxItems':6}}
        if kind == 'action':
            for name,(args,required) in TOOL_ARGUMENTS.items():
                branches.append(object_schema({**common,'tool':{'enum':[name]},'tool_arguments':object_schema(args,required)}))
        elif kind == 'finish':
            branches.append(object_schema({**common,'coverage':coverage}))
        elif kind == 'decompose':
            children = {'type':'array','minItems':1,'maxItems':8,'items':object_schema({'objective':STRING,'requirements':{'type':'array','items':STRING},'allow_decompose':{'type':'boolean'}})}
            branches.append(object_schema({**common,'children':children}))
        else:
            branches.append(object_schema(common))
    return {'type':'object','$defs':{'evidence':evidence,'emptyObject':object_schema({}),
            'emptyArray':{'type':'array','items':{},'maxItems':0}},'anyOf':branches}
