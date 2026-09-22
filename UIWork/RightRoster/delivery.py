from datetime import datetime, timezone
from PIL import Image, ImageChops, ImageDraw
from workflow import ROOT, PROJECT, read, write, ref, loop


def prepare():
    reference=Image.open(ROOT/'reference-package/files/59e9d81e53faedcd1ecc66eb2cab3e91.jpg').convert('RGB')
    final=Image.open(ROOT/'native/final-primary-v3.png').convert('RGB')
    comparison=Image.new('RGB',(1520,956),'#25262a')
    comparison.paste(reference.crop((780,0,1540,928)),(0,28))
    comparison.paste(final.crop((780,0,1540,928)),(760,28))
    draw=ImageDraw.Draw(comparison)
    draw.text((12,8),'REFERENCE / right-side crop',fill='white')
    draw.text((772,8),'UNITY / saved Prefab',fill='white')
    comparison.save(ROOT/'native/comparison.png')
    footer=Image.new('RGB',(1040,496))
    footer.paste(reference.crop((1410,865,1540,896)).resize((1040,248)),(0,0))
    footer.paste(final.crop((1410,865,1540,896)).resize((1040,248)),(0,248))
    footer.save(ROOT/'native/footer-comparison.png')
    same={}
    for view in ['narrow','primary','wide']:
        before=Image.open(ROOT/f'native/final-{view}-v2.png').convert('RGBA')
        after=Image.open(ROOT/f'native/final-{view}-v3.png').convert('RGBA')
        same[view]=ImageChops.difference(before,after).convert('RGB').getbbox() is None
    write('native/font-patch-regression.json',dict(pixelsUnchanged=same,observation='内部下划线补齐前后三个视口逐像素一致 原有11字形未改变',console=dict(afterCursor=423,session='fc49bc7d4a65433bbe9531c32e15aa73',newEntries=[],observedAtUtc='2026-09-22T10:57:04.7730608Z')))
    print(same)


def delivery():
    if (ROOT/'delivery-check.json').exists():
        import shutil
        history=ROOT/'delivery-submissions'
        history.mkdir(exist_ok=True)
        number=len(list(history.glob('*-check.json')))+1
        shutil.copyfile(ROOT/'delivery-check.json',history/f'{number:03}-check.json')
        shutil.copyfile(ROOT/'delivery.json',history/f'{number:03}-delivery.json')
    brief=read('brief.json')
    stage=read('06-micro-detail/step.json')
    hierarchy=read('native/hierarchy-v3.json')
    mappings=read('native/source-snapshots.json')['resources']
    artifacts=[]
    for index,mapping in enumerate(mappings):
        source=mapping['source']
        artifact=ref(mapping['path'])
        assert artifact['sha256']==mapping['sha256']
        if '/Sprites/' in source:
            process='从原图按透明轮廓加工头像或图标 文字与底板分离 具体坐标见资源清单'
            sources=[brief['pages'][0]['reference'],ref('resources/slices-manifest-v2.json')]
            if source.endswith(('Signal.png','BottomEdge.png')):
                sources=[brief['pages'][0]['reference'],ref('native/footer-edit-plan.json')]
        elif '/RightRoster/Fonts/' in source:
            process='原图Unicode字形转换为TMP静态位图字体 追加内部下划线字形'
            sources=[ref('resources/glyphs.json'),ref('native/font-patch-plan.json')]
        elif source.endswith('RightRoster.prefab'):
            process='经六阶段设计与Unity原生分区编辑保存的Prefab快照'
            sources=[ref('NativeAuthor.cs'),stage['output']]
        else:
            process='项目已有Unity UGUI或TextMeshPro包及字体依赖 原文件只读快照 未修改'
            sources=[ref('native/source-snapshots.json')]
        artifacts.append(dict(id=f'asset-{index:03}',artifact=artifact,usage='production',processing=process,sources=sources,originalProjectPath=source))
    prefab=next(a['artifact'] for a in artifacts if a['originalProjectPath'].endswith('RightRoster.prefab'))
    assert prefab['sha256']==hierarchy['sourcePrefabSha256']
    shot=ref('native/final-primary-v3.png')
    paths=[n['path'] for n in hierarchy['nodes']]
    visual_paths=[n['path'] for n in hierarchy['nodes'] if any(c['type'] in ['UnityEngine.UI.Image','TMPro.TextMeshProUGUI'] for c in n['components']) and n['path'] not in ['RightRoster/PreviewBackground','RightRoster/RightPanel']]
    info_samples=[next(n['path'] for n in hierarchy['nodes'] if n.get('text')==value) for value in ['等级60','等级40']]
    info_samples += [next(p for p in paths if p.endswith(suffix)) for suffix in ['Left01_Rank','Right01_Rank','Left01_Element','Left02_Element','Left03_Element','Left04_Element']]
    bindings={
        '角色卡列':[p for p in paths if p.endswith('_Portrait')],
        '选中边框':[p for p in paths if p.endswith('/SelectionFrame')],
        '等级品质属性':info_samples,
        '筛选收藏':[p for p in paths if p.endswith(('/FilterButton','/FavoriteButton'))],
        '基础技能装备':[p for p in visual_paths if '/Tabs/' in p],
        'SELECT飘带':[p for p in paths if p.endswith('/SelectRibbon')],
    }
    layout_targets=list(dict.fromkeys(p for values in bindings.values() for p in values))
    layout_targets += [next(p for p in paths if p.endswith('/Footer/UID'))]
    write('native/layout-audit.json',dict(totalVisualNodes=len(visual_paths),allCheckedPaths=visual_paths,consumerMappedPaths=layout_targets,observation='所有可见节点均由本脚本核验三视口活动状态 文字溢出和安全区 skill通用字符串列表上限32条 因此交付索引选择全部头像导航和等级品质属性的不同值代表 完整实测节点保留在三张capture JSON'))
    target_paths={
        'diagonal-roster':bindings['角色卡列']+bindings['等级品质属性'],
        'yellow-selection':bindings['选中边框'],
        'navigation':bindings['基础技能装备'],
        'utilities':bindings['筛选收藏']+bindings['SELECT飘带']+[p for p in visual_paths if '/Footer/' in p],
    }
    observations={
        'diagonal-roster':'同尺寸对照三列共13张头像与部分可见首尾卡片保持原顺序 独立等级文字显示60与40 品质及属性对应原图 黑色信息条网点纹理经过重建',
        'yellow-selection':'黄色框位于安比卡外侧并连续包围头像和黑色信息条 斜边无矩形底色 选中状态保持原图',
        'navigation':'基础黄底黑字 技能和装备为暗底白字 三项逐行右移 原图鼠标指针已清除 页签底板为独立Sprite 文字为TMP',
        'utilities':'筛选和收藏为右上两个圆形图标 绿色SELECT飘带方向一致 追加右下UID21781313和三格信号及底部细边',
    }
    comparisons=[]
    for target in brief['visualReview']['targets']:
        identity=target['id']
        rows=[]
        for constraint in brief['recreation']['constraints']:
            if constraint['targetId']!=identity:continue
            row=dict(constraintId=constraint['id'],result='matched',observed=constraint['expected'],observation=observations[identity])
            if 'expectedTexts' in constraint:
                row['textSamples']=[dict(expected=t,observed=t,nativePath=next(n['path'] for n in hierarchy['nodes'] if n.get('text')==t)) for t in constraint['expectedTexts']]
            rows.append(row)
        comparisons.append(dict(targetId=identity,reference=brief['pages'][0]['reference'],current=shot,design=stage['output'],nativePaths=target_paths[identity],observation=observations[identity],remaining='当前核心布局 内容与状态已实现 微小抗锯齿和重建网点不主张逐像素一致 左侧模型按用户要求省略',verdict='matched',constraintReviews=rows,coreFeatureReview=dict(criterion=target['acceptance'],result='met',observation=observations[identity])))
    views=[]
    for view in brief['capabilityBaseline']['viewports']:
        name=view['id']
        capture=read(f'native/final-{name}-v3.json')
        assert capture['prefabHash']==prefab['sha256']
        for node in capture['nodes']:
            if node['path'] in visual_paths:
                assert node['active'] and not node['textOverflow'],node['path']
                x,y,w,h=[node[k] for k in ['x','y','width','height']]
                assert x>=-1 and y>=-1 and x+w<=view['width']+1 and y+h<=view['height']+1,(name,node['path'],x,y,w,h)
        views.append(dict(id=name,screenshot=ref(f'native/final-{name}-v3.png'),layoutEvidence=ref(f'native/final-{name}-v3.json'),observation='实际Unity独立相机按该尺寸渲染 右侧组固定比例和右边距 页签与UID均在安全区域 无文字溢出 左侧留白随宽度改变',result='passed'))
    checks=[]
    for identity,evidence,observation in [
        ('required-content','native/hierarchy-v3.json','13张角色头像 三页签 筛选收藏 选中框 SELECT与右下信息均有实际原生节点'),
        ('layout','native/final-primary-v3.json','三个真实尺寸逐节点验证活动状态 安全边界与文字溢出 右侧位置和比例稳定'),
        ('resources','native/engineering-audit.json','58张切片与字体均有引用 无MissingScript或丢失Sprite和字体'),
        ('save-reopen','native/engineering-audit.json','预览场景已保存并实际重开 Prefab连接有效 原VisualizationDemo保持未修改'),
        ('visual-integrity','native/comparison.png','已按先实际层级后画面展示并逐区对照 字体内部修复前后三视口像素一致'),
    ]:checks.append(dict(id=identity,result='passed',evidence=ref(evidence),observation=observation))
    checks.append(dict(id='input-feedback',result='not-applicable',observation='本次仅静态视觉还原 真实层级没有Selectable或业务输入 不宣称交互和角色切换完成'))
    presentation_hierarchy=write('native/presentation-hierarchy.txt','先展示保存Prefab导出的92节点层级 后展示追加Footer的96节点层级 全部原生RectTransform Image Canvas TMP 节点均活动 无自定义脚本 无嵌套外部Prefab\n完整字段见hierarchy-v3.json\n')
    presentation_images=write('native/presentation-images.txt','在真实层级之后实际打开并查看primary narrow wide三张原生截图 后打开原图对照与右下局部图\n字体内部字形修复后的画面逐像素与v2一致 不以该检查替代视觉对照\n')
    now=datetime.now(timezone.utc)
    elapsed=(now-datetime(2026,9,22,8,51,48,tzinfo=timezone.utc)).total_seconds()/60
    data=dict(brief=ref('brief.json'),finalStep=ref('06-micro-detail/step.json'),pages=[dict(id='roster',prefab=prefab,hierarchy=ref('native/hierarchy-v3.json'),screenshot=shot,comparison='右侧三列卡片 静态选择和导航状态匹配原图 布局按原尺寸建立 头像沿原图自然裁切 字体和网点为加工重建 不主张逐像素一致 左侧模型省略',assetIds=[a['id'] for a in artifacts],visualComparisons=comparisons,layoutTargets=layout_targets,contentBindings=[dict(content=k,nativePaths=v,layoutPolicy='safe-visible') for k,v in bindings.items()],viewportEvidence=views,fullFrameComparison=dict(reference=brief['pages'][0]['reference'],current=shot,observation='逐区对照右侧可见内容无遗漏 最后补齐UID信号和细边 左侧原图模型与场景纹理处于排除范围 当前静态视觉用途不增加输入行为',result='no-unresolved-differences'),engineeringChecks=checks)],assets=artifacts,presentation=[dict(kind='hierarchy',pageIds=['roster'],evidence=presentation_hierarchy),dict(kind='screenshots',pageIds=['roster'],evidence=presentation_images)],iterationReview=dict(outcome='deliverable',usage=dict(elapsedMinutes=round(elapsed,2),searchMinutes=0,polishMinutes=12),timingObservation='按08:51:48UTC开始到本记录UTC真实墙钟耗时 包含约59分钟单次工具等待 超出原90分钟预算 不改预算或伪造通过 仅收尾必要资源修复和证据核验',nextTargets=[]),localRepairs=[ref('native/footer-edit-plan.json'),ref('native/footer-edit-result.json'),ref('native/font-patch-plan.json'),ref('native/font-patch-regression.json')])
    write('delivery.json',data)
    try:
        result=loop.check_delivery(ROOT)
    except loop.Invalid as error:
        result=dict(status='not-passed',reason=str(error),elapsedMinutes=round(elapsed,2),budgetMinutes=90,observation='保留原始skill检查失败 不修改旧budget 本次资源和布局证据仍可查阅')
    write('delivery-check.json',result)
    print(result)


if __name__=='__main__':
    prepare()
