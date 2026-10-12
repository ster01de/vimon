# Records the README demo from a real terminal session with https://github.com/charmbracelet/vhs
#
#   1. Put a vimon binary on PATH (release download or `dotnet publish`).
#   2. Run from the repository root:  vhs docs/demo.tape
#
# Values are whatever the recording machine reports at that moment; nothing is staged.

Output docs/assets/vimon-demo.gif

Set Shell "bash"
Set FontSize 15
Set Width 1240
Set Height 560
Set Padding 16
Set TypingSpeed 80ms

# English number formatting, clean prompt.
Hide
Type "export LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 PS1='$ '"
Enter
Type "clear"
Enter
Show

Type "vimon"
Sleep 400ms
Enter

# Let the sparklines fill up, then keep a still frame for the README.
Sleep 8s
Screenshot docs/assets/vimon.png

# Hide rows without a value.
Type "h"
Sleep 3s

# Compact view and back.
Type "c"
Sleep 3s
Type "c"
Sleep 2s

Type "q"
Sleep 1s
