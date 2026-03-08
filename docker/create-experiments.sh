#!/bin/bash
# Create experiments for all 5 tiers
API="http://localhost:5000/api"

TMPFILE=$(mktemp)

for i in 0 1 2 3 4; do
  TIER_NAMES=("P1 Current State" "P2 Semantic Memory" "P3 Anti-Patterns" "P4 Preferences" "P5 Project Facts")
  cat > "$TMPFILE" <<ENDJSON
{"name":"${TIER_NAMES[$i]}: ACL vs Prose","tier":$i,"targetSessions":60}
ENDJSON
  echo -n "Creating experiment for tier $i (${TIER_NAMES[$i]})... "
  RESULT=$(curl -s -X POST "$API/experiments" -H "Content-Type: application/json" -d @"$TMPFILE")
  echo "$RESULT"
done

rm -f "$TMPFILE"
echo ""
echo "Done. Run 'bash compare.sh' to test."
