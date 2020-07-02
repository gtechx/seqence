using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Timeline.Data;
using PlayMode = UnityEngine.Timeline.PlayMode;

namespace UnityEditor.Timeline
{
    [TimelineEditor(typeof(XAnimationTrack))]
    public class EditorAnimTrack : EditorTrack
    {
        protected override Color trackColor
        {
            get { return Color.yellow; }
        }

        private AnimationTrackData data;

        private AnimationTrackData Data
        {
            get
            {
                if (data == null)
                {
                    data = track.data as AnimationTrackData;
                }
                return data;
            }
        }

        private Character ch;

        private Character Character
        {
            get
            {
                if (ch == null && Data != null)
                {
                    int id = Data.roleid;
                    ch = CharacterWindow.Find(id);
                }
                return ch;
            }
        }

        protected override bool warn
        {
            get { return ch == null && trackArg == null; }
        }

        protected override string trackHeader
        {
            get
            {
                if (trackArg is Character c)
                {
                    return c.name + " " + ID;
                }
                else if (Character != null)
                {
                    return Character.name + " " + ID;
                }
                return "角色" + ID;
            }
        }

        protected override List<TrackMenuAction> actions
        {
            get
            {
                var types = TypeUtilities.GetRootChilds(typeof(XAnimationTrack));
                List<TrackMenuAction> ret = new List<TrackMenuAction>();
                for (int i = 0; i < types.Count; i++)
                {
                    var str = types[i].ToString();
                    int idx = str.LastIndexOf('.');
                    if (idx >= 0)
                    {
                        str = str.Substring(idx + 1);
                    }
                    var act = new TrackMenuAction()
                    {
                        desc = "Add " + str, on = false, fun = AddSubTrack, arg = types[i]
                    };
                    ret.Add(act);
                }
                return ret;
            }
        }

        private void AddSubTrack(object arg)
        {
            Type type = (Type) arg;
            var state = TimelineWindow.inst.state;
            EditorFactory.GetTrackByDataType(type, state.timeline, track, (tr, data, param) =>
            {
                if (tr != null && data != null)
                {
                    var tmp = track;
                    if (track.childs?.Length > 0)
                    {
                        tmp = track.childs.Last();
                    }
                    tr.parent.AddSub(tr);
                    tr.parent.AddTrackChildData(data);
                    int idx = TimelineWindow.inst.tree.IndexOfTrack(tmp);
                    TimelineWindow.inst.tree.AddTrack(tr, idx + 1, param);
                }
            });
        }


        protected override void OnGUIHeader()
        {
            XBindTrack btrack = track as XBindTrack;
            if (btrack)
            {
                if (btrack.bindObj == null)
                {
                    btrack.Load();
                }
                EditorGUILayout.ObjectField("", btrack.bindObj, typeof(GameObject), false, GUILayout.MaxWidth(80));
            }
            EditorGUILayout.Space();
        }

        protected override void OnDragDrop(UnityEngine.Object[] objs)
        {
            var selectedObjects = from go in objs where go as AnimationClip != null select go as AnimationClip;
            if (selectedObjects.Count() > 0)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (e.type == EventType.DragPerform)
                {
                    var clip = selectedObjects.First();
                    float t = TimelineWindow.inst.PiexlToTime(e.mousePosition.x);
                    AddClip(clip, t);
                    DragAndDrop.AcceptDrag();
                    e.Use();
                }
            }
        }


        protected override void OnAddClip(float t)
        {
            ObjectSelector.get.Show(null, typeof(AnimationClip), null, false, null, obj =>
            {
                AnimationClip u_clip = (AnimationClip) obj;
                if (u_clip != null)
                {
                    AddClip(u_clip, t);
                }
            }, null);
        }

        private void AddClip(AnimationClip u_clip, float t)
        {
            AnimClipData data = new AnimClipData();
            data.start = t;
            data.duration = u_clip.averageDuration;
            data.anim = AssetDatabase.GetAssetPath(u_clip);
            data.trim_start = 0;
            data.loop = u_clip.isLooping;
            XAnimationTrack atr = (XAnimationTrack) track;
            XAnimationClip clip = new XAnimationClip((XAnimationTrack) track, data);
            clip.aclip = u_clip;
            clip.port = track.clips?.Length ?? 0;
            track.AddClip(clip, data);
        }

        protected override void OnSelect()
        {
            base.OnSelect();
            Selection.activeGameObject = (track as XBindTrack).bindObj;
        }

        protected override void OnInspectorTrack()
        {
            if (TimelineWindow.inst.playMode == PlayMode.Skill)
            {
                var timeline = TimelineWindow.inst.timeline;
                if (timeline.config.skillHostTrack == 0)
                {
                    var root = track.root;
                    int idx = Array.IndexOf(timeline.trackTrees, root);
                    if (idx > 0)
                    {
                        timeline.config.skillHostTrack = (ushort) idx;
                    }
                }
            }
            if (ch == null && trackArg == null) EditorGUILayout.HelpBox("bind character is none", MessageType.Warning);
        }

        protected override void OnInspectorClip(IClip c)
        {
            base.OnInspectorClip(c);
            XAnimationClip xc = c as XAnimationClip;
            var data = c.data as AnimClipData;
            data.loop = EditorGUILayout.Toggle("loop", data.loop);
            data.trim_start = EditorGUILayout.FloatField("start trim", data.trim_start);
            xc.aclip = (AnimationClip) EditorGUILayout.ObjectField("clip", xc.aclip, typeof(AnimationClip), false);
        }

        public override bool AllowClipDrag(DragMode dm, float delta, IClip c)
        {
            var d = c.data as AnimClipData;
            if (dm == DragMode.Left)
            {
                d.trim_start += delta;
                d.trim_start = Mathf.Max(0, d.trim_start);
                return d.trim_start > 1e-2;
            }
            if (dm == DragMode.Right)
            {
                d.trim_end += delta;
                if (Mathf.Abs(d.trim_end) < 1e-1) d.trim_end = 0;
                return true;
            }
            return base.AllowClipDrag(dm, delta, c);
        }

        public override ClipMode CalcuteClipMode(IClip c, out float loopLen)
        {
            var d = c.data as AnimClipData;
            ClipMode mode = ClipMode.None;
            if (d.trim_start > 1e-2) mode |= ClipMode.Left;
            if (d.trim_end < -1e-2)
                mode |= ClipMode.Right;
            else if (d.trim_end > 1e-2) mode |= ClipMode.Loop;
            loopLen = d.trim_end;
            return mode;
        }
    }
}
