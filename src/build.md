## Linux

https://www.booleanworld.com/creating-linux-apps-run-anywhere-appimage/

https://github.com/AppImage/appimagetool

## MacOS

https://github.com/LinusU/node-appdmg

-------------------


brew install create-dmg

create-dmg \
  --volname "Application Installer" \
  --volicon "application_icon.icns" \
  --background "installer_background.png" \
  --window-pos 200 120 \
  --window-size 800 400 \
  --icon-size 100 \
  --icon "Application.app" 200 190 \
  --hide-extension "Application.app" \
  --app-drop-link 600 185 \
  "Application-Installer.dmg" \
  "source_folder/"