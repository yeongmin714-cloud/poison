#!/bin/bash
# Git auto-push with Telegram notification
# Runs every 10 minutes via cron

cd /mnt/c/Unity/code

# Check for uncommitted changes
if [ -z "$(git status --porcelain)" ]; then
    exit 0
fi

# Get change summary
STATS=$(git diff --stat HEAD | tail -1)

# Add all changes
git add -A

# Commit with timestamp
git commit -m "Auto-push: $(date '+%Y-%m-%d %H:%M') - $STATS"

# Push to origin
git push origin master

# Get commit info for notification
HASH=$(git rev-parse --short HEAD)
MSG=$(git log -1 --pretty=%B)

# Send Telegram notification
curl -s -X POST "https://api.telegram.org/bot8853731251:AAHqVqVqVqVqVqVqVqVqVqVqVqVqVqVqVqV/sendMessage" \
    -d chat_id=6847418902 \
    -d text="🔄 Auto-push completed
📁 Project: poison (Unity)
📝 Commit: $HASH - $MSG
📊 Changes: $STATS
⏰ Time: $(date '+%Y-%m-%d %H:%M')" \
    > /dev/null

exit 0