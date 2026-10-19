# dmgbuild settings for hst imager gui dmg
# usage: dmgbuild -s dmg-settings.py -D app=<path to .app> -D background=<path to background> "Hst Imager" <output.dmg>
import os.path

application = defines['app']
appname = os.path.basename(application)

format = 'UDZO'
size = None

# app bundle and link to applications for drag and drop install
files = [application]
symlinks = {'Applications': '/Applications'}
hide_extensions = [appname]

# window layout with app icon left, arrow in background and applications link right
background = defines['background']
window_rect = ((200, 120), (540, 320))
default_view = 'icon-view'
show_status_bar = False
show_tab_view = False
show_toolbar = False
show_pathbar = True
show_sidebar = False
show_icon_preview = False
include_icon_view_settings = True
include_list_view_settings = False

arrange_by = None
label_pos = 'bottom'
text_size = 13
icon_size = 128
icon_locations = {
    appname: (145, 140),
    'Applications': (395, 140)
}
