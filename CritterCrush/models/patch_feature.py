#!/usr/bin/env python3
"""Interim post-step over the scaffolded .feature, standing in for three open Bobcat defects.

Every rewrite here is something the scaffolder should do itself; each is tagged with the issue
that will delete the corresponding branch. Nothing here is a spec-authoring decision — the model
already says all of it.

  bobcat#235  `{streamId}` in a scenario `with:` means the scenario's own stream. The scaffolder
              generates that id and knows it; today it does not substitute it, so the act posts to
              a different stream than the Given events were written to.
  bobcat#231  A collapsed HTTP command slice is driven over HTTP, not the bus:
              `When XRequest is posted to "<route>"`, never `When X is received`.
  bobcat#237  A collapsed HTTP guard refuses with ProblemDetails/400 and throws nothing, so the
              refusal is `Then the response is 400`, not `Then validation fails with …`.
  bobcat#241  `RecordBuilding` demands every constructor parameter, so a Given cannot arrange an
              event partially. The model says which fields the scenario MEANS; the rest are filled
              here with deterministic defaults so the table can be built at all.
"""
import re, sys

model_path, feature_path, source_dir = sys.argv[1], sys.argv[2], sys.argv[3]

# --- read the slice roles out of the model (line-scan; no yaml dependency here) --------------
slices, cur = {}, None
for line in open(model_path):
    m = re.match(r'^  - name: (\S+)', line)
    if m:
        cur = {'name': m.group(1)}
        slices[cur['name']] = cur
        continue
    if cur is None:
        continue
    for key in ('pattern', 'domain', 'command'):
        m = re.match(r'^    %s: (\S+)' % key, line)
        if m:
            cur[key] = m.group(1)
    m = re.match(r'^    trigger: \{ kind: (\w+)', line)
    if m:
        cur['trigger'] = m.group(1)

def over_http(s):
    return s.get('pattern') == 'Command' and s.get('trigger') in ('Http', 'Human')

def route(s):
    return '/api/%s/%s' % (s.get('domain', 'app').lower(), s['name'].lower())

# --- read the scaffolded record shapes, so a partial Given table can be completed (#241) -------
import os, glob
records = {}
for path in glob.glob(os.path.join(source_dir, '*.cs')):
    for name, params in re.findall(r'public record (\w+)\(([^)]*)\);', open(path).read()):
        records[name] = [tuple(p.strip().split()) for p in params.split(',') if p.strip()]

FILLERS = {
    'Guid': '00000000-0000-0000-0000-000000000000',
    'DateTimeOffset': '2026-01-01T00:00:00Z',
    'int': '0', 'long': '0', 'decimal': '0', 'double': '0',
    'bool': 'false', 'string': '',
}

def complete(header, values, stream, aggregate):
    """Every parameter the record needs, in declaration order: what the scenario said, then
    deterministic filler. The scenario's own columns keep their values and their meaning; the
    filler is visibly inert, so a reader can tell at a glance which columns the spec is about."""
    said = {h.lower(): v for h, v in zip(header, values)}
    event = said.pop('event', None)
    params = records.get(event)
    if params is None:
        return header, values
    out_h, out_v = ['Event'], [event]
    for typ, param in params:
        key = param.lower()
        if key in said:
            out_h.append(param); out_v.append(said.pop(key))
        elif key == aggregate.lower() + 'id':
            # The aggregate's own identity IS the stream this scenario arranges (#235)
            out_h.append(param); out_v.append(stream)
        else:
            out_h.append(param); out_v.append(FILLERS.get(typ, ''))
    if said:
        raise SystemExit('columns %s match no parameter of %s' % (sorted(said), event))
    return out_h, out_v

# --- rewrite -----------------------------------------------------------------------------------
out, stream_id, slice_name, changes = [], None, None, {235: 0, 231: 0, 237: 0, 241: 0}
lines = open(feature_path).read().splitlines()
i = -1
while True:
    i += 1
    if i >= len(lines):
        break
    line = lines[i]
    m = re.match(r'^  @slice:(\S+)', line)
    if m:
        slice_name = m.group(1)
    m = re.match(r'^    Given no events for \S+ "([0-9a-f-]+)"', line)
    if m:
        stream_id = m.group(1)

    s = slices.get(slice_name, {})

    # A Given event table: complete it against the record's real parameter list (#241)
    m_table = re.match(r'^    And events for (\S+)$', line)
    if m_table and i + 2 < len(lines):
        header = [c.strip() for c in lines[i + 1].strip().strip('|').split('|')]
        values = [c.strip() for c in lines[i + 2].strip().strip('|').split('|')]
        h2, v2 = complete(header, values, stream_id, m_table.group(1))
        if h2 != header:
            changes[241] += 1
        out.append(line)
        out.append('      | ' + ' | '.join(h2) + ' |')
        out.append('      | ' + ' | '.join(v2) + ' |')
        i += 2
        continue

    if '{streamId}' in line:
        if stream_id is None:
            raise SystemExit('{streamId} before any "Given no events for" step: %r' % line)
        line = line.replace('{streamId}', stream_id)
        changes[235] += 1

    if over_http(s):
        m = re.match(r'^    When (\S+) is received$', line)
        if m:
            line = '    When %sRequest is posted to "%s"' % (m.group(1), route(s))
            changes[231] += 1
        m = re.match(r'^    Then validation fails with ".*"$', line)
        if m:
            line = '    Then the response is 400'
            changes[237] += 1

    out.append(line)

open(feature_path, 'w').write('\n'.join(out) + '\n')
print('  patched: #235 x%d  #231 x%d  #237 x%d  #241 x%d'
      % (changes[235], changes[231], changes[237], changes[241]))
