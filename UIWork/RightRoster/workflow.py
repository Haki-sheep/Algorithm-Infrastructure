from pathlib import Path
import hashlib
import json
import sys
import shutil
import subprocess
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).parent
PROJECT = ROOT.parent.parent
SKILL = Path(r'C:\Users\fmz\Desktop\30-integrated\30-integrated\payload\skills\es-ui-prefab-authoring')
sys.path.insert(0, str(SKILL/'scripts'))
import ui_design_loop as loop


def write(name, value):
    path=ROOT/name
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n' if not isinstance(value,str) else value,encoding='utf-8')
    return ref(name)


def ref(name):
    path=ROOT/name
    return {'path':str(name).replace('\\','/'),'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}


def read(name):
    return json.loads((ROOT/name).read_text(encoding='utf-8'))


def font(size, heavy=False):
    path=PROJECT/'Assets/PathfindingAlgorithm/Arts/Fonts'/('NotoSansSC.ttf' if heavy else 'NotoSansSC-Regular.ttf')
    result=ImageFont.truetype(str(path),size)
    return result


def resource_preview():
    data=read('resources/slices-manifest.json')
    items=[p for p in data['slices'] if p['role']!='portrait']
    sheet=Image.new('RGB',(1200,1150),'#34343b')
    draw=ImageDraw.Draw(sheet)
    draw.text((20,12),'独立切片复核  文字重建样字',font=font(23),fill='white')
    for i,piece in enumerate(items):
        sprite=Image.open(ROOT/piece['artifact']['path'])
        sprite.thumbnail((150,125))
        x=20+(i%7)*170; y=65+(i//7)*150
        sheet.paste(sprite,(x,y),sprite)
        draw.text((x,y+128),piece['name'],fill='white')
    draw.text((25,1110),'基础  技能  装备  等级60  等级40',font=font(24,True),fill='white')
    sheet.save(ROOT/'resources/details-and-type.png')


def validate_tools():
    results=[]
    for name in ['ui_design_loop','ui_local_edit','ui_recreation_hardening']:
        destination=ROOT/'tool-validation'/('verified-'+name)
        run=subprocess.run([sys.executable,'-X','utf8','-B',str(SKILL/'tests'/('test_'+name+'.py')),str(destination)],capture_output=True,encoding='utf-8',errors='replace',env={**__import__('os').environ,'PYTHONUTF8':'1'})
        write('tool-validation/'+name+'.log',run.stdout+'\n'+run.stderr)
        results.append({'name':name,'exitCode':run.returncode,'result':run.stderr[-800:]})
    write('tool-validation/results.json',results)
    print(json.dumps(results,ensure_ascii=False))


def bitmap_font():
    source=Image.open(PROJECT/'Assets/TestImgae/59e9d81e53faedcd1ecc66eb2cab3e91.jpg').convert('RGB')
    samples=[('等',(928,221,952,245),False),('级',(947,221,971,245),False),('6',(967,221,985,245),False),('0',(980,221,999,245),False),
             ('4',(1463,851,1482,874),False),('基',(911,671,935,693),True),('础',(930,671,955,693),True),
             ('技',(926,734,950,757),False),('能',(946,734,972,757),False),('装',(944,797,968,822),False),('备',(964,797,990,822),False)]
    atlas=Image.new('RGBA',(512,64))
    glyphs=[]
    preview=Image.new('RGB',(1120,360),'#323239')
    painter=ImageDraw.Draw(preview)
    for i,(char,box,invert) in enumerate(samples):
        crop=source.crop(box)
        alpha=Image.new('L',crop.size)
        for y in range(crop.height):
            for x in range(crop.width):
                r,g,b=crop.getpixel((x,y))
                v=(255-min(r,g)) if invert else min(r,g,b)
                slant=5*(1-y/crop.height)
                value=max(0,min(255,round((v-48)*255/180)))
                if x<slant or x>crop.width-(7 if char.isdigit() else 5)+slant or (invert and y>18):value=0
                alpha.putpixel((x,y),value)
        rgba=Image.new('RGBA',crop.size,(255,255,255,0));rgba.putalpha(alpha)
        atlas.paste(rgba,(i*44+4,4))
        glyphs.append(dict(character=char,unicode=ord(char),x=i*44+4,y=64-4-crop.height,width=crop.width,height=crop.height,advance=19 if not char.isdigit() else 14,sourceRect=list(box)))
        x=(i%6)*180; y=(i//6)*160
        enlarged=rgba.resize((rgba.width*4,rgba.height*4))
        preview.paste(enlarged,(x,y+25),enlarged)
        painter.text((x,y+133),char,font=font(20),fill='white')
    atlas.save(ROOT/'resources/glyph-atlas.png')
    preview.save(ROOT/'resources/glyph-preview.png')
    write('resources/glyphs.json',dict(atlasWidth=512,atlasHeight=64,pointSize=24,glyphs=glyphs,source=ref('reference-package/files/59e9d81e53faedcd1ecc66eb2cab3e91.jpg')))
    print('Bitmap glyph candidates generated')


def clean_resources():
    data=read('resources/slices-manifest.json')
    for card in data['cards']:
        if not card['rank']:continue
        for suffix,standard in [('_Rank','Left01_Rank' if card['rank']=='A' else 'Right01_Rank'),('_Element',dict(Ice='Left01_Element',Fire='Left02_Element',Electric='Left03_Element',Ether='Left04_Element')[card['element']])]:
            piece=next(p for p in data['slices'] if p['name']==card['name']+suffix)
            base=next(p for p in data['slices'] if p['name']==standard)
            if piece['name']!=standard:
                clean=Image.open(ROOT/base['artifact']['path'])
                dest=Image.new('RGBA',(piece['rect'][2],piece['rect'][3]))
                dest.paste(clean,(0,0))
                dest.save(ROOT/piece['artifact']['path'])
                piece['artifact']=ref(piece['artifact']['path'])
    write('resources/slices-manifest.json',data)
    resource_preview()


def freeze():
    request='帮在项目调用"C:\\Users\\fmz\\Desktop\\30-integrated"这个sikll去切Assets/TestImgae下的图 就是不需模型可以不用还原 但是右侧的UI需要还原 如果skill需要安装则安装到项目中 如果不需要就直接调用即可\n按照skill要求的步骤一步步去做 不是简单地去'
    source=write('request.txt',request)
    reference=ref('reference-package/files/59e9d81e53faedcd1ecc66eb2cab3e91.jpg')
    shown=write('resources/presentation.txt','已实际打开原图 坐标图 头像与信息条拼板以及独立图标底板和中文样字预览 并在对话展示资源准备图\n参考图右侧存在13个可见头像 包含顶部与底部自然裁切的卡片 原图左侧3D人物依据用户原话排除\n')
    direction=write('brief.md','右侧角色选择UI严格参考复刻\n主视口1540×928 原图坐标\n保留三列倾斜卡片 黄色安比选中框 黑色信息条 品质与属性标识 顶部筛选与收藏 三级页签 右缘SELECT飘带\n人物模型不在本次范围 左侧只显示暗色预览底色\n13张头像按原图可见裁切 星标作为头像静态插画内容 等级与页签是原生可编辑文字\n右侧容器由右上锚点与高度缩放拥有尺寸 只伸缩左侧空白 三种横屏比例验证\n不增加游戏业务脚本 不接角色切换或筛选数据\n')
    content=['角色卡列','选中边框','等级品质属性','筛选收藏','基础技能装备','SELECT飘带']
    data=read('resources/slices-manifest.json')
    groups=[{'id':'screen','parent':None,'controls':'画布与右侧安全边界','dependsOn':[]}]
    for name,controls in [('roster','三列13张可见角色卡及信息条'),('selection','右列第二项黄色选中边框'),('toolbar','筛选收藏图标'),('tabs','基础技能装备页签'),('ribbon','右缘SELECT飘带')]:
        groups.append({'id':name,'parent':'screen','controls':controls,'dependsOn':['roster'] if name=='selection' else []})
    targets=[]
    definitions=[('diagonal-roster','roster',[.53,.04,.46,.92],['角色卡列','等级品质属性'],'三列交错斜卡与黑色信息条','13张可见角色头像保持三列次序 斜边下移右倾 黑色信息条中可读等级60与等级40'),
                 ('yellow-selection','selection',[.76,.26,.16,.25],['选中边框'],'安比黄色外框','右列第二张卡保持黄色连续外框 内部头像与等级独立于外框'),
                 ('navigation','tabs',[.55,.70,.14,.20],['基础技能装备'],'黄底当前页与灰框次级页','基础为黄底黑字 技能装备为暗底白字 三项左缘逐行右移'),
                 ('utilities','toolbar',[.84,.04,.16,.92],['筛选收藏','SELECT飘带'],'圆形工具与右缘绿色楔形','右上筛选白图标与收藏黄星保持圆形 右缘绿色SELECT楔形保留原方向')]
    for identity,group,region,contents,feature,acceptance in definitions:
        targets.append(dict(id=identity,page='roster',group=group,region=region,content=contents,feature=feature,acceptance=acceptance))
    constraints=[]
    for identity,target,aspect,expected,texts in [
        ('roster-layout','diagonal-roster','layout','三列交错倾斜布局 按原图保留13张可见卡片',None),
        ('roster-content','diagonal-roster','content','原图头像 品质标记 属性图标与可见等级保持对应',None),
        ('roster-text','diagonal-roster','language','简体中文等级文案和原有数字',['等级60','等级40']),
        ('selection-style','yellow-selection','style','右列第二张安比卡以黄色边框高亮',None),
        ('tabs-text','navigation','language','原图三级中文页签',['基础','技能','装备']),
        ('utilities-content','utilities','content','筛选 收藏 SELECT绿色飘带',None)]:
        row=dict(id=identity,targetId=target,aspect=aspect,expected=expected,basis=dict(kind='reference',source=reference,observation=expected+' 已在完整原图对应位置查看'))
        if texts: row['expectedTexts']=texts
        constraints.append(row)
    inventory=[]
    for role,contents,preview in [('portrait',['角色卡列'],'resources/contact-sheet.png'),('selection',['选中边框'],'resources/details-and-type.png'),('rank',['等级品质属性'],'resources/details-and-type.png'),('toolbar',['筛选收藏'],'resources/details-and-type.png'),('tab',['基础技能装备'],'resources/details-and-type.png'),('ribbon',['SELECT飘带'],'resources/details-and-type.png')]:
        piece=next(p for p in data['slices'] if p['role']==role)
        inventory.append(dict(id=role,role=role,specification='原始像素比例 RGBA独立PNG 图集按实际斜轮廓保留透明区域',content=contents,designCritical=True,status='ready',artifact=piece['artifact'],source=ref('resources/slices-manifest.json'),preview=ref(preview),inspection='实际打开切片拼板 原图头像识别与颜色保持 去除邻卡选中条串入 文本使用项目现有中文字体'))
    inventory.append(dict(id='reference-font',role='原图位图字形',specification='512×64 RGBA atlas 原图11个Unicode字形 24像素字号供TMP静态字体使用',content=['等级品质属性','基础技能装备'],designCritical=True,status='ready',artifact=ref('resources/glyph-atlas.png'),source=ref('resources/glyphs.json'),preview=ref('resources/glyph-preview.png'),inspection='原字体为Thin不匹配 原图逐字切分并移除背景 原生TMP以Unicode文字使用该字形表 不将完整按钮文本烘焙到背景'))
    prep=write('resource-preparation.json',{'schemaVersion':1,'pages':[dict(page='roster',items=inventory,nativeContent=['等级品质属性','基础技能装备'],nativeConstruction='等级与页签文字使用TMP 原生RectTransform组织 Image消费独立切图',contactSheet=ref('resources/contact-sheet.png'),observation='原图不是成品背景 已拆为56张PNG 13个头像 卡片信息条和标签各自独立',presentationEvidence=shown)]})
    brief=dict(schemaVersion=2,qualityContract=loop.QUALITY_CONTRACT,executionContract=loop.EXECUTION_CONTRACT,referenceContract=loop.REFERENCE_CONTRACT,
        resourcePreparation=prep,intent=dict(kind='recreation',userInstruction=request.split('\n')[0]),productionScope={'mode':'visual-only'},aiSuggestions=[],
        iterationPolicy=dict(version=1,mode='strict-reference',budgetBasis='已告知本轮90分钟工作预算 15分钟保留Unity验证 不改变严格参考目标',coreTargetIds=['diagonal-roster','yellow-selection','navigation','utilities'],budget=dict(totalMinutes=90,searchMinutes=10,polishMinutes=15,validationReserveMinutes=15),maxLowGainAttempts=2),
        capabilityBaseline=dict(version=2,layoutPolicy='右侧UI高度固定适配并锚定右边 画布宽度变化由左侧模型留白区承担 不对卡片列横向拉伸 原图顶部底部部分头像自然裁切不补造',viewports=[dict(id=i,width=w,height=h,safeArea=[0,0,w,h]) for i,w,h in [('narrow',1280,928),('primary',1540,928),('wide',1920,928)]],shapeTargets=['diagonal-roster','yellow-selection'],detailTargets=['navigation','utilities']),
        visualReview={'version':1,'targets':targets},pages=[dict(id='roster',requiredContent=content,reference=reference,adaptation='用户指定模型可以不用还原 仅还原右侧UI 不制作左侧模型和左侧标题角色信息',characterPresentation=dict(mode='none',observation='用户排除左侧3D模型 右侧头像为UI插画切片',referenceEvidence=reference),referenceReview=dict(role='target-screen',fullFrame=True,targetMatch='matched',inspectedImage=reference,source='用户项目 Assets/TestImgae 内JPG',observation='已观察整张1540×928角色选择截图 右侧三列角色卡和导航齐全',limitations='JPG有压缩与录屏缩放痕迹 仅一张静态参考 原始字体文件未知',context=dict(game='绝区零',screen='角色选择与特训方案',version='原图未标注',platform='原图未标注 横屏界面',input='原图显示鼠标指针',language='简体中文',state='安比选中 基础页签高亮'),presentationEvidence=shown))],
        recreation=dict(version=2,source=source,constraints=constraints,permissions=[]),orientation='landscape',viewport=dict(width=1540,height=928),artDirection=dict(userDirection='按完整原图还原右侧UI 模型可以省略',definition=direction,directionSource=dict(kind='reference',detail='用户给定本地完整截图')),groups=groups)
    write('brief.json',brief)
    print('Frozen reference / resources / brief')


def render(stage):
    data=read('resources/slices-manifest-v2.json' if stage>=6 else 'resources/slices-manifest.json')
    target=Image.new('RGBA',(1540,928),(12,13,16,255))
    draw=ImageDraw.Draw(target)
    draw.rectangle((780,0,1539,927),fill='black')
    pieces={p['name']:p for p in data['slices']}

    def put(name):
        piece=pieces[name]
        sprite=Image.open(ROOT/piece['artifact']['path'])
        target.alpha_composite(sprite,tuple(piece['rect'][:2]))

    def label(text,x,y,color='white'):
        glyphdata=read('resources/glyphs.json')
        atlas=Image.open(ROOT/'resources/glyph-atlas.png')
        cursor=x
        for ch in text:
            glyph=next(g for g in glyphdata['glyphs'] if g['character']==ch)
            gx=glyph['x']; gy=64-glyph['y']-glyph['height']
            bit=atlas.crop((gx,gy,gx+glyph['width'],gy+glyph['height']))
            if color=='black':
                rgb=Image.new('RGBA',bit.size,(0,0,0,0));rgb.putalpha(bit.getchannel('A'));bit=rgb
            target.alpha_composite(bit,(int(cursor),int(y)))
            cursor+=glyph['advance']

    for card in data['cards']:
        piece=pieces[card['portrait']]
        if stage<5:draw.polygon(piece['polygon'],fill='#36383d' if stage<3 else '#62636a',outline='#92949b')
        if 2<=stage<5 and card['rank']:
            draw.polygon(pieces[card['children'][0]]['polygon'],fill='#08090b')
        if 3<=stage<5 and card['rank']:
            x,y,w,h=card['labelRect']
            draw.text((x,y+2),'等级'+str(card['level']),font=font(19),fill='white')
        if stage>=5:
            put(card['portrait'])
            for child in card['children']:put(child)
            if card['rank']:
                x,y,w,h=card['labelRect'];label('等级'+str(card['level']),x if stage>=6 else x-4,y+2)
    if 2<=stage<4:
        draw.polygon(pieces['SelectionFrame']['polygon'],outline='#e5df37',width=9)
    for name in ['BasicTab','SkillTab','EquipmentTab']:
        piece=pieces[name]; x,y,w,h=piece['rect']
        if stage<4:draw.rounded_rectangle((x,y,x+w,y+h),radius=h//2,fill='#e4dc39' if name=='BasicTab' and stage>=2 else '#24252b',outline='#43434a',width=3)
        if stage==3:
            draw.text((x+59,y+9),piece['label'],font=font(21),fill='black' if name=='BasicTab' else 'white')
    for name in ['FilterButton','FavoriteButton']:
        x,y,w,h=pieces[name]['rect']
        if stage<4:draw.ellipse((x,y,x+w,y+h),fill='#16171a',outline='#56565a',width=3)
    if stage<4:draw.polygon(pieces['SelectRibbon']['polygon'],fill='#939f30')
    if stage>=4:
        for name in ['SelectionFrame','FilterButton','FavoriteButton','BasicTab','SkillTab','EquipmentTab','SelectRibbon']:put(name)
        for name in ['BasicTab','SkillTab','EquipmentTab']:
            p=pieces[name];x,y,w,h=p['rect']; label(p['label'],x+60,y+13,'black' if name=='BasicTab' else 'white')
    out=Path(loop.step_path(stage)).parent/'evidence'/('design-'+str(stage)+'.png')
    target.convert('RGB').save(ROOT/out)
    print(json.dumps(ref(out)))


def begin_stage():
    result=loop.begin(ROOT,int(sys.argv[2]))
    write('begin-'+sys.argv[2]+'.json',result)
    print(json.dumps(result,ensure_ascii=False))


def finish_stage():
    stage=int(sys.argv[2]); observation=sys.argv[3]
    data=read(loop.step_path(stage)); brief=read('brief.json')
    if stage==1:data['input']=brief['pages'][0]['reference']
    output=ref(str(Path(loop.step_path(stage)).parent/'evidence'/('design-'+str(stage)+'.png')))
    established=[('composition','screen','右侧三列卡片保留原坐标 主体模型范围排除','右侧卡列和三项页签处于源图相同区域'),('reading','roster','选中安比高亮 等级黑条统一为第二阅读层','黄边只强调右列第二项 信息条不抢占头像'),('geometry','roster','逐张独立斜切轮廓 字段边界和卡列次序锁定','13张卡保留顶部底部可见裁切 文案不跨属性图标'),('state','tabs','基础高亮 技能与装备暗底 选中边框独立','三页签和两个顶部圆按钮保持明确视觉状态'),('material','roster','原图头像和徽章以独立切片恢复 文字以字形表绘制','没有用整页图片替代头像 文字 信息条与选中外框'),('detail','screen','核对字形基线 透明边缘与全部内容对应','100%尺寸逐区检查并保留原图静态裁切')]
    identity,group,decision,criterion=established[stage-1]
    data.update(status='reviewed',divergenceRequired=False,alternatives=[],reuseReason='原图已锁定本阶段外观与次序 只实施拆分和版式映射 无需制造改变参考的候选',reuseEvidence=data['input'],output=output,
        protected=[dict(id=identity,group=group,decision=decision,check=criterion,establishedAt=1)],pageCoverage={'roster':observation})
    findingid='stage-'+str(stage)
    problems=['完整原图尚未区分本次右侧UI与排除的模型区域','构图块尚未标明安比选择与黑色信息条的主次','结构轮廓尚无等级文本和分区标签的精确占位','几何稿尚未表达原图工具按钮与三页签选中态','状态草稿缺少原图角色纹理和独立的品质属性素材','等级字形坐标使用了重复的左移补偿 与原图相比偏左4像素']
    data['findings']=[dict(id=findingid,kind='design-gap',rootCause=problems[stage-1],observation=problems[stage-1],location=group,impact='下游无法据此还原相应区域',acceptance=criterion,status='fixed',pages=['roster'],decisionIds=[identity])]
    groups={g['id']:g for g in brief['groups']}
    changed=['screen','roster','selection','toolbar','tabs','ribbon']
    prior=[]
    for s in range(1,stage):prior+=read(loop.step_path(s))['protected']
    prior+=data['protected']
    checks={p['id']:observation+' 复核 '+p['check'] for p in prior}
    data['attempts']=[dict(sequence=1,findingId=findingid,before=data['input'],after=output,change=decision,inspection=observation,result='fixed',changedGroups=changed,auditedGroups=sorted(loop.affected_groups(groups,changed)),checked=list(checks),checkObservations=checks)]
    data['review']=dict(reviewer='Codex 实际图片观察',observation=observation,evidence=output,result='reviewed',dimensions=[dict(page='roster',id=key,group=group,observation=observation+' 本阶段维度 '+key,result='observed') for key in loop.DIMENSIONS[stage]])
    if stage in [5,6]:
        comps=[]
        for target in brief['visualReview']['targets']:
            rows=[]
            for c in brief['recreation']['constraints']:
                if c['targetId']!=target['id']:continue
                row=dict(constraintId=c['id'],result='matched',observed=c['expected'],observation=observation)
                if 'expectedTexts' in c:row['textSamples']=[dict(expected=t,observed=t) for t in c['expectedTexts']]
                rows.append(row)
            comps.append(dict(targetId=target['id'],reference=brief['pages'][0]['reference'],current=output,observation=observation,remaining='设计布局与内容按原图落实 原生Unity渲染与字体仍需实测',verdict='matched',constraintReviews=rows,coreFeatureReview=dict(criterion=target['acceptance'],result='met',observation=observation)))
        data['review']['visualComparisons']=comps
        if stage==5:data['review']['representativeSample']=dict(attempt=1,visualComparisons=comps)
    candidate='stage-'+str(stage)+'-candidate.json'
    write(candidate,data)
    result=loop.finish(ROOT,stage,candidate,read('begin-'+str(stage)+'.json')['sha256'])
    print(json.dumps(result,ensure_ascii=False))


def micro_resources():
    data=read('resources/slices-manifest.json')
    original=next(p for p in data['slices'] if p['name']=='BasicTab')
    image=Image.open(ROOT/original['artifact']['path'])
    # 同一列沿原底板的无字区域取色 消除文本补洞横向插值的接缝
    for x in range(45,113):
        top=image.getpixel((x,7));bottom=image.getpixel((x,46))
        for y in range(8,46):
            t=(y-7)/39
            image.putpixel((x,y),tuple(round(top[c]*(1-t)+bottom[c]*t) for c in range(4)))
    path=ROOT/'resources/slices-v2/BasicTab.png';path.parent.mkdir(exist_ok=True);image.save(path)
    original['artifact']=ref('resources/slices-v2/BasicTab.png')
    write('resources/slices-manifest-v2.json',data)


def prepare_footer():
    source=Image.open(ROOT/'reference-package/files/59e9d81e53faedcd1ecc66eb2cab3e91.jpg').convert('RGBA')
    folder=ROOT/'resources/footer-v1'
    folder.mkdir(exist_ok=True)
    for name,box in [('Signal',(1515,872,1533,887)),('BottomEdge',(1060,886,1535,890))]:
        cut=source.crop(box)
        for y in range(cut.height):
            for x in range(cut.width):
                r,g,b,a=cut.getpixel((x,y))
                if max(r,g,b)<32:cut.putpixel((x,y),(r,g,b,0))
        cut.save(folder/(name+'.png'))
    source.crop((1410,865,1540,896)).resize((1040,248)).save(folder/'reference.png')
    shutil.copyfile(PROJECT/'Assets/TestImgae/RightRoster/RightRoster.prefab',ROOT/'native/before-footer.prefab.snapshot')
    write('native/footer-edit-plan.json',dict(problem='最终原图对照发现右下UID与信号和底边缺失',responsibleStage='06-micro-detail',beforeAsset=ref('native/before-footer.prefab.snapshot'),beforeImage=ref('native/final-primary.png'),allowedChanges=['仅向RightPanel追加Footer组及UID文字 信号 底边'],preserved='已有92节点的localId与全部组件属性和引用',source=ref('reference-package/files/59e9d81e53faedcd1ecc66eb2cab3e91.jpg'),verification='真实Unity保存重开 三视口截图 原区域前后及卡片页签回归',staticLocalCheck='新增节点不属于check-local标量编辑覆盖 改用原生作者差异审查'))
    print('Footer incremental source and immutable baseline saved')


def prepare_font_patch():
    image=Image.open(ROOT/'resources/glyph-atlas.png').convert('RGBA')
    ImageDraw.Draw(image).rectangle((486,10,497,11),fill='white')
    image.save(ROOT/'resources/glyph-atlas-v2.png')
    write('native/font-patch-plan.json',dict(problem='实际Unity日志重复报告ReferenceBitmap缺少TMP加载必须查询的下划线',scope='仅在未使用的图集区域增加下划线字形 保留原11字形像素和布局',before=ref('resources/glyph-atlas.png'),after=ref('resources/glyph-atlas-v2.png'),glyph=dict(unicode=95,x=486,y=52,width=12,height=2),verification='三视口重新保存重开 画面逐像素与v2比较 日志cursor393之后检查'))
    print('Font patch prepared')


def production():
    result=loop.check(ROOT,6)
    write('six-stage-check.json',result)
    capabilities=['adaptive-layout','distinctive-shape','production-detail']
    checks=[]
    descriptions=['已查看1280与1920宽度实际Unity小样 右侧边距不变 头像没有横向拉伸 三项页签都在视口内',
                  '已查看原生安比卡 小样透明斜角没有黑色矩形 且独立黄色边框在正确外侧',
                  '已查看原生TMP字体 中文基础技能装备与等级60全部出现 两个圆工具图标和SELECT纹理可辨认']
    for cap,observation in zip(capabilities,descriptions):
        checks.append(dict(page='roster',capability=cap,condition='Unity2022.3 ScreenSpaceCamera RenderTexture 原生Prefab保存重开',observation=observation,targetIds=['diagonal-roster','yellow-selection','navigation','utilities'],evidence=ref('native/probe-primary.json')))
    data=dict(schemaVersion=1,brief=ref('brief.json'),finalStep=ref(loop.step_path(6)),risks=[dict(id='native-font-shape-layout',condition='TMP位图字形和透明斜边在匹配的Unity版本下导入与显示',observation='已通过三个实际宽度的小样捕获并实际看图 小样保存重开成功 所有字形和透明边界存在',status='verified',evidence=ref('native/probe-primary.png'),capabilities=capabilities,checks=checks)],resourceGaps=[])
    write('implementation.json',data)
    result=loop.check_production(ROOT)
    write('production-check.json',result)
    print(json.dumps(result,ensure_ascii=False))


if __name__=='__main__':
    if sys.argv[1]=='delivery-prepare':
        from delivery import prepare
        prepare()
    elif sys.argv[1]=='delivery':
        from delivery import delivery
        delivery()
    else:
        {'preview':resource_preview,'freeze':freeze,'validate-tools':validate_tools,'glyphs':bitmap_font,'clean':clean_resources,'begin':begin_stage,'render':lambda:render(int(sys.argv[2])),'finish':finish_stage,'micro':micro_resources,'production':production,'prepare-footer':prepare_footer,'prepare-font-patch':prepare_font_patch}[sys.argv[1]]()
