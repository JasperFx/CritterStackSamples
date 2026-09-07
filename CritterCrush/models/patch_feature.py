#!/usr/bin/env python3
"""Interim post-step over the scaffolded .feature, standing in for two open Bobcat defects.

Every rewrite here is mechanical, derived from the model, and something the scaffolder should do
itself; each is tagged with the issue that will delete it. Nothing here is a spec-authoring
decision. 0.13.0 already absorbed the other two (#231, #237) — the branches for those are gone.

  bobcat#235  `{streamId}` in a scenario `with:` means the scenario's own stream. The scaffolder
              generates that id and knows it; today it does not substitute it, so the act posts to
              a different stream than the Given events were written to — and the refusal scenarios
              then pass whether or not the guard exists.
  bobcat#241  `RecordBuilding` demands every constructor parameter, so a Given cannot arrange an
              event partially. The model says which fields the scenario MEANS; the rest are filled
              here with deterministic, visibly inert values so the table can be built at all.
"""
import glob
import os
import re
import sys

feature_path, source_dir = sys.argv[1], sys.argv[2]

# --- the scaffolded record shapes, so a partial Given table can be completed (#241) ------------
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
    deterministic filler. The scenario's own columns keep their values and their meaning, and the
    filler is visibly inert, so a reader can still tell which columns the spec is about."""
    said = {h.lower(): v for h, v in zip(header, values)}
    event = said.pop('event', None)
    params = records.get(event)
    if params is None:
        return header, values

    out_h, out_v = ['Event'], [event]
    for typ, param in params:
        key = param.lower()
        if key in said:
            out_h.append(param)
            out_v.append(said.pop(key))
        elif key == aggregate.lower() + 'id':
            # The aggregate's own identity IS the stream this scenario arranges (#235)
            out_h.append(param)
            out_v.append(stream)
        else:
            out_h.append(param)
            out_v.append(FILLERS.get(typ, ''))

    if said:
        raise SystemExit('columns %s match no parameter of %s' % (sorted(said), event))
    return out_h, out_v


lines = open(feature_path).read().splitlines()
out, stream_id, changes, i = [], None, {235: 0, 241: 0}, -1

while True:
    i += 1
    if i >= len(lines):
        break
    line = lines[i]

    m = re.match(r'^    Given no events for \S+ "([0-9a-f-]+)"', line)
    if m:
        stream_id = m.group(1)

    table = re.match(r'^    And events for (\S+)$', line)
    if table and i + 2 < len(lines):
        header = [c.strip() for c in lines[i + 1].strip().strip('|').split('|')]
        values = [c.strip() for c in lines[i + 2].strip().strip('|').split('|')]
        h2, v2 = complete(header, values, stream_id, table.group(1))
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

    out.append(line)

open(feature_path, 'w').write('\n'.join(out) + '\n')
print('  patched: #235 x%d  #241 x%d' % (changes[235], changes[241]))
