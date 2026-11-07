using System;
using UnityEngine;
namespace FunctionalHorrorProps {
    /// <summary>Small gallery browser; uses OnGUI events with either Unity input backend.</summary>
    public sealed class HorrorPropsDemo : MonoBehaviour {
        [SerializeField] private FunctionalHorrorProp[] props = Array.Empty<FunctionalHorrorProp>();
        [SerializeField] private Camera viewCamera;
        [SerializeField] private bool showControls = true;
        private Rect panel;
        private Vector2 scroll;
        private int selected;
        public void Configure(FunctionalHorrorProp[] entries, Camera camera) {props=entries??Array.Empty<FunctionalHorrorProp>();viewCamera=camera;}
        public void Focus(int index) {
            if(props.Length==0)return;selected=Mathf.Clamp(index,0,props.Length-1);
            Camera camera=viewCamera!=null?viewCamera:Camera.main;if(camera==null)return;
            var renderers=props[selected].GetComponentsInChildren<Renderer>();if(renderers.Length==0)return;
            Bounds bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            float size=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
            // Room for doors and drawer extension, plus a steady stationary floor reference.
            float distance=Mathf.Max(.9f,size*2.8f);
            camera.transform.position=bounds.center+new Vector3(1.2f,.8f,1.7f).normalized*distance;
            camera.transform.LookAt(bounds.center);camera.fieldOfView=40f;
        }
        private void OnGUI() {
            if(props==null)return;panel=new Rect(16,16,340,Mathf.Min(Screen.height-32,640));
            if(showControls){GUILayout.BeginArea(panel,"THE NIGHT SHIFT / INTERACTIVE PROPS",GUI.skin.window);
                GUILayout.Label("Choose a prop to inspect. Activate to play; deactivate to stop or close.");
                scroll=GUILayout.BeginScrollView(scroll);
                for(int i=0;i<props.Length;i++){var p=props[i];if(p==null)continue;
                    GUILayout.BeginHorizontal();int index=i;
                    if(GUILayout.Button(p.DisplayName,GUILayout.Height(31)))Focus(index);
                    if(p.HasInteraction){if(GUILayout.Button(p.IsActive?"Stop":"Activate",GUILayout.Width(76),GUILayout.Height(31)))p.Toggle();}
                    else GUILayout.Label("Static",GUILayout.Width(76),GUILayout.Height(31));
                    GUILayout.EndHorizontal();}
                GUILayout.EndScrollView();
                if(GUILayout.Button("Stop all animations"))foreach(var p in props)if(p!=null)p.Deactivate();
                GUILayout.EndArea();}
            var current=Event.current;
            if(current.type!=EventType.MouseDown||current.button!=0||(showControls&&panel.Contains(current.mousePosition)))return;
            Camera cam=viewCamera!=null?viewCamera:Camera.main;if(cam==null)return;
            Ray ray=cam.ScreenPointToRay(new Vector3(current.mousePosition.x,Screen.height-current.mousePosition.y,0));
            if(!Physics.Raycast(ray,out RaycastHit hit,100))return;
            var prop=hit.collider.GetComponentInParent<FunctionalHorrorProp>();if(prop==null||!prop.HasInteraction)return;prop.Toggle();current.Use();
        }
    }
}
