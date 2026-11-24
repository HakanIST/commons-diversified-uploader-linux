#!/bin/sh
# Runs the published app under Xvfb and captures each step in both languages.
# Used by CI to check the interface on a real Ubuntu; needs xvfb, dbus and imagemagick.
# Usage: packaging/screenshots.sh <app-directory> <output-directory>
set -eu
app="$1/commons-diversified-uploader"
out="$2"
mkdir -p "$out"
photos=$(mktemp -d)
for i in 1 2 3 4 5; do
  convert -size 1500x1000 "gradient:#1f2978-#fcdb33" -gravity center -pointsize 220 -fill white -annotate 0 "$((i * 7))" "$photos/SAMPLE$i.JPG"
done
shot() { # <name> <seconds> <env…>
  name="$1"; seconds="$2"; shift 2
  env "$@" GSK_RENDERER=cairo GDK_BACKEND=x11 XDG_CACHE_HOME="$photos/.cache" XDG_CONFIG_HOME="$photos/.config" \
    xvfb-run -a -s "-screen 0 1280x860x24" dbus-run-session -- sh -c "
      \"$app\" > \"$out/$name.log\" 2>&1 &
      pid=\$!
      sleep $seconds
      import -window root \"$out/$name.png\" || true
      kill \$pid 2>/dev/null || true
    "
}
shot splash-tr 2.6 CDU_LANG=tr
shot welcome-en 6 CDU_LANG=en
for lang in tr en; do
  for step in match roster photos upload; do
    shot "$step-$lang" 8 CDU_LANG=$lang CDU_FOLDER="$photos" CDU_STEP=$step
  done
done
ls -la "$out"
