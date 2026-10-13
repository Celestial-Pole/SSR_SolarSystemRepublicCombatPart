"""从原动画源码导出运行曲线，保留时间、切线和权重，不启动 Unity。"""
import json
import re
from pathlib import Path
import yaml

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / 'src/SSR.Combat.UnityAsset/Assets'


def read_clip(path):
    #移除 Unity 的文档标签，实际动画数据按 YAML 读取。
    source = re.sub(r'^%.*\n|^--- !u!.*\n', '', path.read_text(encoding='utf-8'), flags=re.M)
    return yaml.safe_load(source)['AnimationClip']


def export_clip(path):
    #只导出实际运动曲线；编辑器曲线是同一数据的另一种表示，不能重复应用。
    clip = read_clip(path)
    tracks = []
    for key, kind, axes in [('m_PositionCurves', 'position', 'xyz'),
                            ('m_RotationCurves', 'rotation', 'xyzw'),
                            ('m_ScaleCurves', 'scale', 'xyz'),
                            ('m_EulerCurves', 'euler', 'xyz')]:
        for track in clip.get(key, []):
            curve = track['curve']
            keys = curve['m_Curve']
            tracks.append({'path': track['path'], 'kind': kind, 'keys': [
                {'time': frame['time'], 'value': [frame['value'][a] for a in axes],
                 'incoming': [frame['inSlope'][a] for a in axes],
                 'outgoing': [frame['outSlope'][a] for a in axes],
                 'weighted': frame.get('weightedMode', 0),
                 'inWeight': [frame.get('inWeight', {}).get(a, 1 / 3) for a in axes],
                 'outWeight': [frame.get('outWeight', {}).get(a, 1 / 3) for a in axes]}
                for frame in keys]})
    if clip.get('m_CompressedRotationCurves') or clip.get('m_FloatCurves') or clip.get('m_PPtrCurves'):
        raise ValueError(f'动画包含尚未转换的通道：{path}')
    if not tracks:
        raise ValueError(f'动画没有可用的位姿曲线：{path}')
    if any(key['weighted'] != 0 for track in tracks for key in track['keys']):
        raise ValueError(f'动画包含尚未转换的加权切线：{path}')
    stop = clip.get('m_AnimationClipSettings', {}).get('m_StopTime')
    return {'name': clip['m_Name'], 'length': stop or max(t['keys'][-1]['time'] for t in tracks),
            'source': path.relative_to(ASSETS).as_posix(), 'tracks': tracks}


def main():
    #运行时使用的独立后坐和九组井盖动画均须存在，缺少任何一个都终止构建。
    names = {'500mmRecoil', 'EMSentryRecoil', 'PortableEMSentryRecoil',
             'EMAutocannonRecoil', 'Railgun145Recoil', 'Railgun300Recoil'}
    names.update(f'SiloOpening{i:02}' for i in range(1, 10))
    found = {p.stem: p for p in ASSETS.rglob('*.anim') if p.stem in names}
    if names != found.keys():
        raise ValueError(f'缺少动画：{names - found.keys()}')
    output = ROOT / '1.7/AnimationData'
    output.mkdir(parents=True, exist_ok=True)
    for name, path in sorted(found.items()):
        data = export_clip(path)
        (output / (name + '.json')).write_text(json.dumps(data, ensure_ascii=False, separators=(',', ':'),
                                                       allow_nan=False), encoding='utf-8')
        print(name, data['length'], len(data['tracks']))


if __name__ == '__main__':
    main()
