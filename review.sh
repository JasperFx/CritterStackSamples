#!/bin/bash
# review.sh <relative/path.cs> — the scaffold on the left, what we made of it on the right.
#
# Paths are relative to .scaffold-output/, e.g. Scheduling/ConfirmAppointment.cs or
# Specs/BookingAppointmentsSpecs.cs. Runs from any directory.
#
# The implementation is found by EXISTENCE, not by diff's exit code: diff exits 1 whenever the two
# files differ, which is the normal case, so `diff a b || diff a c` ran the fallback every time a
# slice had been implemented and reported a missing file.
set -u
cd "$(dirname "$0")"
f="${1:?usage: review.sh <path under .scaffold-output/>}"

scaffold=".scaffold-output/$f"
[ -f "$scaffold" ] || { echo "no scaffold file: $scaffold" >&2; exit 2; }

case "$f" in
  Specs/*) implemented="CritterCrush/CritterCrush.Specs/$f" ;;
  *)       implemented="CritterCrush/CritterCrush/$f" ;;
esac
[ -f "$implemented" ] || { echo "no implemented file: $implemented" >&2; exit 2; }

diff -y --width=200 "$scaffold" "$implemented"
[ $? -le 1 ]   # 0 = identical, 1 = differs; only a diff failure (2) is an error
