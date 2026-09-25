#!/bin/bash
# review.sh <relative/path.cs>  — the scaffold on the left, what we made of it on the right
f="$1"
diff -y --width=200 ".scaffold-output/$f" "CritterCrush/CritterCrush/$f" 2>/dev/null \
  || diff -y --width=200 ".scaffold-output/$f" "CritterCrush/CritterCrush.Specs/$f"
