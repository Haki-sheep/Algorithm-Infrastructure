from pathlib import Path
import json
import hashlib
import sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = Path(__file__).parent
PROJECT = ROOT.parent.parent
SOURCE = PROJECT / 'Assets/TestImgae/59e9d81e53faedcd1ecc66eb2cab3e91.jpg'


def save_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def bind(path):
    return {'path': path.relative_to(ROOT).as_posix(), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}


def inspect():
    folder = ROOT / 'resources'
    folder.mkdir(parents=True, exist_ok=True)
    source = Image.open(SOURCE).convert('RGB')
    draw = ImageDraw.Draw(source)
    for x in range(800, 1540, 50):
        draw.line((x, 0, x, 928), fill='#37ffff', width=1)
        draw.text((x+2, 25), str(x), fill='white')
    for y in range(0, 928, 50):
        draw.line((800, y, 1540, y), fill='#37ffff', width=1)
        draw.text((800, y+2), str(y), fill='white')
    source.crop((790, 0, 1540, 928)).save(folder / 'coordinate-inspection.png')
    Image.open(SOURCE).crop((830, 650, 1080, 865)).resize((750, 645)).save(folder / 'tabs-inspection.png')
    Image.open(SOURCE).crop((866, 212, 1025, 258)).resize((954, 276)).save(folder / 'glyph-inspection.png')


def slice_assets():
    source = Image.open(SOURCE).convert('RGBA')
    folder = ROOT / 'resources/slices-v1'
    folder.mkdir(parents=True, exist_ok=True)
    pieces = []

    def polygon(name, points, image=None, role='portrait'):
        x0, y0 = min(p[0] for p in points), min(p[1] for p in points)
        x1, y1 = max(p[0] for p in points), max(p[1] for p in points)
        crop = (image or source).crop((x0, y0, x1, y1))
        mask = Image.new('L', (crop.width*4, crop.height*4))
        ImageDraw.Draw(mask).polygon([((x-x0)*4, (y-y0)*4) for x, y in points], fill=255)
        crop.putalpha(mask.resize(crop.size, Image.Resampling.LANCZOS))
        crop.save(folder / f'{name}.png')
        entry = {'name': name, 'role': role, 'rect': [x0, y0, x1-x0, y1-y0], 'polygon': points,
                 'artifact': bind(folder / f'{name}.png')}
        pieces.append(entry)
        return entry

    def rectangle(name, box, role):
        x0, y0, x1, y1 = box
        return polygon(name, [(x0,y0),(x1,y0),(x1,y1),(x0,y1)], role=role)

    cards = [
        ('Left01', 823,47,966,1018,217, 871,255,'A',60,'Ice'),
        ('Left02', 883,258,1036,1083,423, 931,464,'A',60,'Fire'),
        ('Left03', 943,468,1090,1140,633, 992,674,'S',60,'Electric'),
        ('Left04',1005,679,1153,1204,845,1054,890,'A',60,'Ether'),
        ('Middle00',981,38,1128,1146,95,997,134,'S',60,'Ice'),
        ('Middle01',1013,141,1159,1205,305,1061,347,'S',60,'Fire'),
        ('Middle02',1074,354,1224,1269,515,1124,557,'S',60,'Ice'),
        ('Middle03',1138,562,1283,1334,726,1187,766,'S',60,'Electric'),
        ('Middle04',1199,773,1345,1380,890,1231,890,'',0,''),
        ('Right01',1140,48,1288,1333,212,1187,250,'S',60,'Fire'),
        ('Right02',1195,257,1340,1387,422,1243,458,'A',60,'Electric'),
        ('Right03',1262,470,1410,1458,635,1311,674,'S',60,'Ice'),
        ('Right04',1324,680,1470,1523,847,1373,889,'A',40,'Electric')]
    layout = []
    for name,x,y,right,bottomright,bottom,leftbottom,footbottom,rank,level,element in cards:
        points = [(x,y),(right,y),(bottomright,bottom),(leftbottom,bottom)]
        if name == 'Middle01':
            points = [(x,y),(right,y),(1188,247),(1177,248),(1193,bottom),(leftbottom,bottom)]
        elif name == 'Middle02':
            points = [(x,y),(1206,y),(1235,451),(1257,470),(bottomright,bottom),(leftbottom,bottom)]
        portrait = polygon(name+'_Portrait',points)
        row = {'name':name,'portrait':portrait['name'],'rect':portrait['rect'],'rank':rank,'level':level,
               'element':element, 'selected':name=='Right02', 'children':[]}
        if rank:
            footer_right = bottomright + (footbottom-bottom)*0.28
            footer_left = leftbottom + (footbottom-bottom)*0.28
            points=[(leftbottom,bottom),(bottomright,bottom),(round(footer_right),footbottom),(round(footer_left),footbottom)]
            clean=source.copy()
            painter=ImageDraw.Draw(clean)
            painter.polygon(points, fill=(10,10,12,255))
            for py in range(bottom,footbottom,4):
                for px in range(leftbottom,round(footer_right),4):
                    if (px//4+py//4)%2 == 0:
                        painter.rectangle((px,py,px+1,py+1),fill=(20,20,22,255))
            footer=polygon(name+'_Footer',points,image=clean,role='footer')
            badge=rectangle(name+'_Rank',(leftbottom+1,bottom-7,leftbottom+39,min(footbottom,bottom+32)),'rank')
            icon=rectangle(name+'_Element',(bottomright-23,bottom+2,bottomright+3,bottom+30),'element')
            row['children']=[footer['name'],badge['name'],icon['name']]
            row['labelRect']=[leftbottom+57,bottom+2,66,30]
        layout.append(row)

    for piece in pieces:
        if piece['role'] not in ('rank','element'):
            continue
        path=folder/(piece['name']+'.png')
        crop=Image.open(path)
        mask=Image.new('L',crop.size)
        for py in range(crop.height):
            for px in range(crop.width):
                red,green,blue,alpha=crop.getpixel((px,py))
                visible=(red>110 and green>65 and blue<red*0.8) if piece['role']=='rank' else (max(red,green,blue)>65 and max(red,green,blue)-min(red,green,blue)>45)
                if visible:
                    mask.putpixel((px,py),255)
        # 填充徽章内部黑色字形 外沿扩展一像素保留描边
        flood=mask.copy()
        for px in range(crop.width):
            for py in (0,crop.height-1):
                if flood.getpixel((px,py))==0:
                    ImageDraw.floodfill(flood,(px,py),128)
        for py in range(crop.height):
            for px in (0,crop.width-1):
                if flood.getpixel((px,py))==0:
                    ImageDraw.floodfill(flood,(px,py),128)
        mask=flood.point(lambda v: 0 if v==128 else 255).filter(ImageFilter.MaxFilter(3))
        if piece['name']=='Middle01_Element':
            ImageDraw.Draw(mask).polygon([(18,0),(26,0),(26,28),(26,28)],fill=0)
        crop.putalpha(mask)
        crop.save(path)
        piece['artifact']=bind(path)

    selected=polygon('SelectionFrame',[(1178,248),(1337,248),(1352,257),(1410,467),(1263,467),(1245,452)],role='selection')
    frame=Image.open(folder/'SelectionFrame.png')
    alpha=frame.getchannel('A')
    painter=ImageDraw.Draw(alpha)
    painter.polygon([(1195-1178,257-248),(1340-1178,257-248),(1397-1178,458-248),(1253-1178,458-248)],fill=0)
    frame.putalpha(alpha)
    frame.save(folder/'SelectionFrame.png')
    selected['artifact']=bind(folder/'SelectionFrame.png')
    for name,box in [('FilterButton',(1308,54,1359,105)),('FavoriteButton',(1376,54,1428,106))]:
        entry=rectangle(name,box,'toolbar')
        crop=Image.open(folder/f'{name}.png')
        mask=Image.new('L',(crop.width*4,crop.height*4))
        ImageDraw.Draw(mask).ellipse((4,4,crop.width*4-4,crop.height*4-4),fill=255)
        crop.putalpha(mask.resize(crop.size,Image.Resampling.LANCZOS))
        crop.save(folder/f'{name}.png')
        entry['artifact']=bind(folder/f'{name}.png')
    for name,box,label in [('BasicTab',(850,657,1003,708),'基础'),('SkillTab',(869,721,1022,768),'技能'),('EquipmentTab',(888,785,1040,832),'装备')]:
        entry=rectangle(name,box,'tab')
        crop=Image.open(folder/f'{name}.png')
        # 清除原文字与鼠标指针 保留端帽轮廓和底板渐变
        for py in range(8,crop.height-5):
            for px in range(47,111):
                left=crop.getpixel((43,py)); right=crop.getpixel((114,py))
                ratio=(px-43)/71
                crop.putpixel((px,py),tuple(round(left[c]*(1-ratio)+right[c]*ratio) for c in range(4)))
        mask=Image.new('L',(crop.width*4,crop.height*4))
        painter=ImageDraw.Draw(mask)
        painter.rounded_rectangle((0,0,crop.width*4+80,crop.height*4),radius=crop.height*2,fill=255)
        crop.putalpha(mask.resize(crop.size,Image.Resampling.LANCZOS))
        crop.save(folder/f'{name}.png')
        entry['artifact']=bind(folder/f'{name}.png')
        entry['label']=label
    polygon('SelectRibbon',[(1537,39),(1537,890),(1420,469)],role='ribbon')
    save_json(ROOT/'resources/slices-manifest.json',{'source':bind(ROOT/'reference-package/files'/SOURCE.name),
              'processing':'人工核对像素坐标 多边形透明裁切 信息条重建 文字交由原生TMP 鼠标指针清除',
              'slices':pieces,'cards':layout,'viewport':[1540,928]})
    sheet=Image.new('RGB',(1200,1060),'#34343b')
    painter=ImageDraw.Draw(sheet)
    font=ImageFont.truetype(str(PROJECT/'Assets/PathfindingAlgorithm/Arts/Fonts/NotoSansSC-Regular.ttf'),20)
    painter.text((20,10),'资源准备  13 张头像与信息条  独立图标 / 边框 / 页签底板',font=font,fill='white')
    for i,card in enumerate(layout):
        crop=Image.open(folder/(card['portrait']+'.png'))
        crop.thumbnail((190,210))
        sx=20+(i%6)*196; sy=55+(i//6)*258
        sheet.paste(crop,(sx,sy),crop)
        painter.text((sx,sy+213),card['name'],font=font,fill='white')
    for i,piece in enumerate([p for p in pieces if p['role'] in ['toolbar','tab','selection','rank','element']][:14]):
        crop=Image.open(folder/(piece['name']+'.png'))
        crop.thumbnail((125,95))
        sx=20+(i%7)*168; sy=824+(i//7)*112
        sheet.paste(crop,(sx,sy),crop)
        painter.text((sx,sy+92),piece['name'][:14],fill='white')
    sheet.save(ROOT/'resources/contact-sheet.png')
    print(json.dumps({'slices':len(pieces),'cards':len(layout),'folder':str(folder)},ensure_ascii=False))


if __name__ == '__main__':
    slice_assets() if len(sys.argv)>1 and sys.argv[1]=='slice' else inspect()
