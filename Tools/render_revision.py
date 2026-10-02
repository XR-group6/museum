from pathlib import Path
import json,sys
from PIL import Image,ImageDraw
from render_preview import draw_character,font,ROOT
old=ROOT.parent/'Incense_Characters'
im=Image.new('RGB',(1800,920),(242,240,233));dr=ImageDraw.Draw(im)
dr.text((50,32),'SHOULDER CONSTRUCTION & ADULT HAIR / V1 TO V2',font=font(28),fill=(37,46,42))
for folder,name,cx,label in [(old,'Father_Young',230,'V1 / shoulder cap'),(ROOT,'Father_Young',680,'V2 / connected shoulder'),(old,'Daughter_Adult',1120,'V1 / shared hair base'),(ROOT,'Daughter_Adult',1570,'V2 / fitted side-part bob')]:
 d=json.loads((folder/'Assets/IncenseCharacters/Source'/f'{name}.json').read_text());draw_character(im,d,cx,1550,780,angle=-14)
 dr=ImageDraw.Draw(im);dr.rectangle((cx-220,825,cx+220,920),fill=(242,240,233));dr.text((cx-200,850),label,font=font(22),fill=(37,46,42))
im.save(ROOT/'Previews/Revision_Comparison.png')
im=Image.new('RGB',(1500,850),(242,240,233));dr=ImageDraw.Draw(im);dr.text((45,30),'ADULT DAUGHTER / HAIR & WARDROBE',font=font(28),fill=(37,46,42))
d=json.loads((ROOT/'Assets/IncenseCharacters/Source/Daughter_Adult.json').read_text())
for cx,angle,lab in [(250,0,'FRONT'),(750,90,'SIDE'),(1250,180,'BACK')]:
 draw_character(im,d,cx,1280,660,angle=angle)
 dr=ImageDraw.Draw(im);dr.rectangle((cx-240,770,cx+240,850),fill=(242,240,233));dr.text((cx-65,798),lab,font=font(22),fill=(37,46,42))
im.save(ROOT/'Previews/Adult_Daughter_Views.png')
