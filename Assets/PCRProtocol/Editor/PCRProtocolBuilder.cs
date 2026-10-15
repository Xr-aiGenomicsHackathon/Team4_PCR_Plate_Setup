#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public static class PCRProtocolBuilder
{
    const string Folder="Assets/PCRProtocol";
    static PCRProtocolController flow;
    static Material teal,blue,amber;
    static TMP_FontAsset font;
    static Sprite ring;
    static void Resources()
    {
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets","PCRProtocol");
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if(!font)throw new Exception("TMP essential font missing");
        teal=Mat("Protocol Teal",new Color(.12f,.75f,.58f));blue=Mat("Protocol Liquid",new Color(.12f,.5f,.95f));amber=Mat("Protocol Amber",new Color(1,.62f,.12f));
        ring=AssetDatabase.LoadAllAssetsAtPath(Folder+"/ProgressRing.asset").OfType<Sprite>().FirstOrDefault();
        if(!ring){var tex=new Texture2D(64,64,TextureFormat.RGBA32,false);tex.name="Progress ring texture";for(int y=0;y<64;y++)for(int x=0;x<64;x++){float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(32,32));tex.SetPixel(x,y,new Color(1,1,1,d>22&&d<30?1:0));}tex.Apply();AssetDatabase.CreateAsset(tex,Folder+"/ProgressRing.asset");ring=Sprite.Create(tex,new Rect(0,0,64,64),Vector2.one*.5f,64);ring.name="Progress ring";AssetDatabase.AddObjectToAsset(ring,tex);}
    }
    static Material Mat(string name,Color color)
    {
        string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m)return m;
        m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.name=name;m.color=color;m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*.08f);AssetDatabase.CreateAsset(m,path);return m;
    }
    static GameObject New(string name,Transform parent=null)
    {
        var g=new GameObject(name);Undo.RegisterCreatedObjectUndo(g,"Build PCR protocol");if(parent)g.transform.SetParent(parent,false);return g;
    }
    static Bounds Bounds(GameObject g)
    {
        var rs=g.GetComponentsInChildren<Renderer>(true).Where(r=>!(r is TMPro.TMP_SubMesh)&&!(r is TMPro.TMP_SubMeshUI)&&r.GetComponent<TMP_Text>()==null).ToArray();if(rs.Length==0)return new Bounds(g.transform.position,Vector3.one*.01f);var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;
    }
    static void Place(GameObject g,Vector3 centerBottom)
    {
        Undo.RecordObject(g.transform,"Place protocol equipment");var b=Bounds(g);g.transform.position+=new Vector3(centerBottom.x-b.center.x,centerBottom.y-b.min.y,centerBottom.z-b.center.z);PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);
    }
    static GameObject Primitive(string name,PrimitiveType type,Transform parent,Vector3 pos,Vector3 size,Material mat,bool collision=false)
    {
        var g=GameObject.CreatePrimitive(type);Undo.RegisterCreatedObjectUndo(g,"Build PCR protocol");g.name=name;g.transform.SetParent(parent,true);g.transform.position=pos;
        var s=parent?parent.lossyScale:Vector3.one;g.transform.localScale=new Vector3(size.x/s.x,size.y/s.y,size.z/s.z);g.GetComponent<Renderer>().sharedMaterial=mat;
        if(!collision)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;
    }
    static Transform Anchor(string name,Transform parent,Vector3 point,Quaternion rot)
    {var g=New(name,parent);g.transform.SetPositionAndRotation(point,rot);return g.transform;}
    static TMP_Text Label(string name,Transform parent,string text,Vector3 point,float width=.11f)
    {
        var g=New(name,parent);g.transform.position=point;g.transform.rotation=Quaternion.Euler(0,90,0);
        var t=g.AddComponent<TextMeshPro>();t.font=font;t.text=text;t.fontSize=Mathf.Clamp(width*4,.1f,.5f);t.enableWordWrapping=false;t.overflowMode=TextOverflowModes.Overflow;t.alignment=TextAlignmentOptions.Center;t.rectTransform.sizeDelta=new Vector2(width,.035f);t.color=Color.white;
        var s=parent.lossyScale;g.transform.localScale=new Vector3(1/s.x,1/s.y,1/s.z);return t;
    }
    static XRGrabInteractable Grab(GameObject g,bool kinematic=true)
    {
        var rb=g.GetComponent<Rigidbody>();if(!rb)rb=Undo.AddComponent<Rigidbody>(g);rb.isKinematic=kinematic;rb.useGravity=false;rb.mass=.05f;rb.interpolation=RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
        var grab=g.GetComponent<XRGrabInteractable>();if(!grab)grab=Undo.AddComponent<XRGrabInteractable>(g);
        var col=g.GetComponent<BoxCollider>();if(!col){var b=Bounds(g);col=Undo.AddComponent<BoxCollider>(g);Vector3 min=new Vector3(float.MaxValue,float.MaxValue,float.MaxValue),max=new Vector3(float.MinValue,float.MinValue,float.MinValue);for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2){var p=g.transform.InverseTransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z)));min=Vector3.Min(min,p);max=Vector3.Max(max,p);}col.center=(min+max)*.5f;col.size=Vector3.Max(max-min,Vector3.one*.001f);}
        col.contactOffset=.001f;grab.colliders.Clear();grab.colliders.Add(col);grab.useDynamicAttach=true;grab.snapToColliderVolume=false;grab.movementType=XRBaseInteractable.MovementType.Kinematic;grab.throwOnDetach=false;
        if(!grab.attachTransform)grab.attachTransform=Anchor("Grip",g.transform,Bounds(g).center,Quaternion.identity);
        return grab;
    }
    static PCRProtocolItem Item(GameObject g,PCRProtocolItem.ItemKind kind,string identity)
    {var item=g.GetComponent<PCRProtocolItem>();if(!item)item=Undo.AddComponent<PCRProtocolItem>(g);item.kind=kind;item.identity=identity;Grab(g);return item;}
    static PCRProtocolSocket Socket(string name,Transform parent,Vector3 point,Quaternion rot,float radius,PCRProtocolSocket.SocketRole role)
    {
        var go=New(name,parent);go.transform.SetPositionAndRotation(point,rot);var s=parent.lossyScale;go.transform.localScale=new Vector3(1/s.x,1/s.y,1/s.z);
        var trigger=go.AddComponent<SphereCollider>();trigger.radius=radius;trigger.isTrigger=true;
        var socket=go.AddComponent<PCRProtocolSocket>();socket.flow=flow;socket.role=role;socket.attachTransform=Anchor("Snap pose",go.transform,point,rot);socket.showInteractableHoverMeshes=false;socket.recycleDelayTime=.25f;return socket;
    }
    public static void BuildStage1()
    {
        if(GameObject.Find("PCR Protocol Flow"))throw new Exception("Protocol root already exists");Resources();
        flow=New("PCR Protocol Flow").AddComponent<PCRProtocolController>();
        flow.audioSource=flow.gameObject.AddComponent<AudioSource>();flow.audioSource.playOnAwake=false;flow.audioSource.spatialBlend=0;flow.audioSource.volume=.45f;
        flow.instructionClips[0]=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sterile Pipette tips.mp3");flow.instructionClips[2]=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Vortex Mixer m.mp3");
        var panelGo=new GameObject("PCR Protocol Panel",typeof(RectTransform));Undo.RegisterCreatedObjectUndo(panelGo,"Create protocol panel");panelGo.transform.SetParent(flow.transform,false);flow.panel=panelGo.AddComponent<PCRProtocolPanel>();flow.panel.flow=flow;flow.panel.font=font;flow.panel.ringSprite=ring;flow.panel.Build();flow.panel.Place();
        var pipRoot=GameObject.Find("Pipette");flow.pipette=pipRoot.GetComponentsInChildren<XRGrabInteractable>().OrderByDescending(g=>g.transform.position.z).First();
        var body=flow.pipette.GetComponentsInChildren<Renderer>().OrderBy(r=>r.bounds.min.y).First().bounds;
        flow.pipetteNozzle=Anchor("Protocol Nozzle",flow.pipette.transform,new Vector3(body.center.x,body.min.y,body.center.z),Quaternion.identity);
        flow.tipSocket=Socket("Sterile Tip Socket",flow.pipette.transform,flow.pipetteNozzle.position,Quaternion.identity,.012f,PCRProtocolSocket.SocketRole.Tip);
        var pr=Bounds(flow.pipette.gameObject);var dial=Primitive("Protocol Volume Dial",PrimitiveType.Cylinder,flow.pipette.transform,new Vector3(pr.center.x,pr.max.y+.004f,pr.center.z),new Vector3(.022f,.005f,.022f),teal,true);
        dial.AddComponent<XRSimpleInteractable>();dial.AddComponent<PCRProtocolDial>().flow=flow;
        flow.pipetteDisplay=Label("Pipette Volume Display",flow.pipette.transform,"1 µL",new Vector3(pr.center.x+.025f,pr.max.y-.02f,pr.center.z),.055f);
        var tips=GameObject.Find("Pipette Tip Box");tips.transform.Find("Box_Lid").gameObject.SetActive(false);
        var tipObjects=tips.transform.Cast<Transform>().Where(t=>t.name.StartsWith("Tip_")).Where((t,i)=>i%6==0).Take(16).ToArray();
        foreach(var t in tipObjects){var item=Item(t.gameObject,PCRProtocolItem.ItemKind.Tip,"");t.gameObject.AddComponent<PCRProtocolTipFilter>().flow=flow;var b=Bounds(t.gameObject);item.Grab.attachTransform.SetPositionAndRotation(new Vector3(b.center.x,b.max.y,b.center.z),Quaternion.identity);item.liquidPoint=Anchor("Liquid contact",t,new Vector3(b.center.x,b.min.y,b.center.z),Quaternion.identity);}
        var rack=GameObject.Find("CodeRackTube");var master=rack.transform.Find("MASTER MIX").gameObject;master.transform.SetParent(flow.transform,true);flow.masterMix=Item(master,PCRProtocolItem.ItemKind.MasterMix,"MM");Place(master,new Vector3(-1.62f,.848f,.43f));
        var template=rack.transform.Find("DNASample").gameObject;var samples=new List<GameObject>{template};for(int i=1;i<7;i++){var clone=UnityEngine.Object.Instantiate(template,rack.transform);Undo.RegisterCreatedObjectUndo(clone,"Create sample/control tube");samples.Add(clone);}
        for(int i=0;i<samples.Count;i++){var name=i<5?"S"+(i+1):i==5?"PTC":"NTC";samples[i].name="Protocol Tube "+name;Item(samples[i],PCRProtocolItem.ItemKind.Sample,name);Place(samples[i],new Vector3(-1.655f+i*.017f,.859f,.355f));Label("Tube label "+name,samples[i].transform,name,Bounds(samples[i]).center+Vector3.right*.01f,.025f);}
        Label("Master Mix Label",master.transform,"MM",Bounds(master).center+Vector3.right*.012f,.035f);
        var mixer=GameObject.Find("mixer");Place(mixer,new Vector3(-1.75f,.848f,.425f));var mb=Bounds(mixer);var tube=Bounds(master);
        flow.vortexSocket=Socket("Vortex Tube Socket",mixer.transform,new Vector3(mb.center.x,mb.max.y+tube.extents.y+.002f,mb.center.z),Quaternion.identity,.028f,PCRProtocolSocket.SocketRole.Vortex);
        Label("Vortex Label",mixer.transform,"VORTEX",new Vector3(mb.max.x+.01f,mb.center.y,mb.center.z),.08f);
        flow.plate=Item(GameObject.Find("pcr_plate"),PCRProtocolItem.ItemKind.Plate,"PLATE");
        foreach(var rb in flow.plate.GetComponentsInChildren<Rigidbody>(true))if(rb.gameObject!=flow.plate.gameObject)Undo.DestroyObjectImmediate(rb);
        foreach(var grab in flow.plate.GetComponentsInChildren<XRGrabInteractable>(true))if(grab!=flow.plate.Grab){Undo.RecordObject(grab,"Retire legacy plate grabs");grab.enabled=false;}
        foreach(var behaviour in flow.plate.GetComponentsInChildren<MonoBehaviour>(true))if(behaviour is PCRWellMapStep||behaviour is PCRWellColumnSelector||behaviour is PCRWellColumnColliderBinder||behaviour is PCRCoverAttemptOpener){Undo.RecordObject(behaviour,"Retire legacy plate sequence");behaviour.enabled=false;}
        flow.plate.GetComponent<Animator>().enabled=false;flow.plate.transform.Find("Cover").gameObject.SetActive(false);flow.plate.Grab.enabled=false;
        flow.seal=Item(GameObject.Find("OpticalSeal"),PCRProtocolItem.ItemKind.Seal,"SEAL");
        flow.materialGroups=new[]{tips,flow.plate.gameObject,flow.seal.gameObject,master,rack};
        flow.legacyPanels=UnityEngine.Resources.FindObjectsOfTypeAll<Transform>().Where(t=>t.gameObject.scene.IsValid()&&(t.name=="PCR Guide World Panel"||t.name=="PCR Plate Map World Panel"||t.name=="PCR Column Name Popup")).Select(t=>t.gameObject).ToArray();foreach(var g in flow.legacyPanels)g.SetActive(false);
        foreach(var guide in UnityEngine.Resources.FindObjectsOfTypeAll<PCRGuideUIController>().Where(c=>c.gameObject.scene.IsValid()))guide.enabled=false;
        var waste=Primitive("PLACEHOLDER Tip Waste Bin",PrimitiveType.Cylinder,flow.transform,new Vector3(-1.41f,.89f,.43f),new Vector3(.09f,.04f,.09f),amber,true);
        flow.wasteOpening=Anchor("Waste opening",waste.transform,new Vector3(-1.41f,.934f,.43f),Quaternion.identity);Label("Waste label",flow.transform,"TIP WASTE",new Vector3(-1.35f,.9f,.43f),.12f);
        EditorUtility.SetDirty(flow);EditorUtility.SetDirty(flow.panel);EditorSceneManager.MarkSceneDirty(flow.gameObject.scene);AssetDatabase.SaveAssets();
    }
    public static void BuildStage2()
    {
        Resources();flow=UnityEngine.Object.FindFirstObjectByType<PCRProtocolController>();if(flow.targets.Length>0)throw new Exception("Transfer targets already built");
        var all=new List<PCRProtocolTarget>();
        foreach(var item in flow.materialGroups[4].GetComponentsInChildren<PCRProtocolItem>(true).Concat(new[]{flow.masterMix}))
        {
            var cap=item.transform.Find("Cap");if(cap)cap.gameObject.SetActive(false);var b=Bounds(item.gameObject);var port=New("Aspirate "+item.identity,item.transform);port.transform.position=new Vector3(b.center.x,b.max.y+.005f,b.center.z);var target=port.AddComponent<PCRProtocolTarget>();target.kind=PCRProtocolTarget.TargetKind.Source;target.identity=item.identity;target.flow=flow;target.radius=.03f;all.Add(target);
            Primitive("Tube opening cue",PrimitiveType.Sphere,port.transform,port.transform.position,Vector3.one*.012f,teal);
        }
        var map=flow.plate.GetComponent<PCRWellMapStep>();var so=new SerializedObject(map);var bindings=so.FindProperty("wellBindings");
        for(int i=0;i<bindings.arraySize;i++)
        {
            var b=bindings.GetArrayElementAtIndex(i);string coord=b.FindPropertyRelative("coordinate").stringValue;var r=(Renderer)b.FindPropertyRelative("renderer").objectReferenceValue;
            var go=New("Transfer well "+coord,r.transform);go.transform.position=new Vector3(r.bounds.center.x,r.bounds.max.y+.003f,r.bounds.center.z);
            var target=go.AddComponent<PCRProtocolTarget>();target.kind=PCRProtocolTarget.TargetKind.Well;target.index=Array.IndexOf(flow.wellNames,coord);target.identity=coord;target.radius=.006f;target.flow=flow;all.Add(target);
            var cue=Primitive("Well cue "+coord,PrimitiveType.Cylinder,go.transform,go.transform.position,new Vector3(.004f,.0002f,.004f),blue);
            if(target.index>=0){var liquid=Primitive("Liquid "+coord,PrimitiveType.Cylinder,flow.plate.transform,go.transform.position-Vector3.up*.002f,new Vector3(.004f,.0001f,.004f),blue);flow.liquidVisuals[target.index]=liquid.transform;liquid.SetActive(false);}
        }
        flow.targets=all.ToArray();EditorUtility.SetDirty(flow);EditorSceneManager.MarkSceneDirty(flow.gameObject.scene);
    }
    public static void BuildStage3()
    {
        Resources();flow=UnityEngine.Object.FindFirstObjectByType<PCRProtocolController>();if(flow.inspectionRoot)throw new Exception("Finishing interactions already built");
        var b=Bounds(flow.plate.gameObject);var inspection=New("Plate Inspection Markers",flow.plate.transform);flow.inspectionRoot=inspection;
        var targets=flow.targets.ToList();string[] checks={"BUBBLES","VOLUMES","POSITIONS"};
        for(int i=0;i<3;i++){var pos=new Vector3(b.max.x+.018f,b.max.y+.012f,b.center.z+(i-1)*.045f);var marker=Primitive("Inspect "+checks[i],PrimitiveType.Sphere,inspection.transform,pos,Vector3.one*.022f,amber,true);marker.AddComponent<XRSimpleInteractable>();var t=marker.AddComponent<PCRProtocolTarget>();t.flow=flow;t.kind=PCRProtocolTarget.TargetKind.Inspection;t.index=i;targets.Add(t);Label("Check label",marker.transform,checks[i],pos+Vector3.up*.02f,.07f);}
        flow.sealSocket=Socket("Optical Seal Socket",flow.plate.transform,new Vector3(b.center.x,b.max.y+.001f,b.center.z),flow.seal.Grab.attachTransform.rotation,.065f,PCRProtocolSocket.SocketRole.Seal);
        var edges=New("Seal Edge Press Targets",flow.plate.transform);flow.edgesRoot=edges;
        for(int i=0;i<4;i++)
        {
            Vector3 pos=b.center+new Vector3(i<2?(i==0?-b.extents.x:b.extents.x):0,b.extents.y+.006f,i>=2?(i==2?-b.extents.z:b.extents.z):0);
            var marker=Primitive("Press Seal Edge "+(i+1),PrimitiveType.Cube,edges.transform,pos,i<2?new Vector3(.012f,.008f,.07f):new Vector3(.05f,.008f,.012f),teal,true);marker.AddComponent<XRSimpleInteractable>();var target=marker.AddComponent<PCRProtocolTarget>();target.flow=flow;target.kind=PCRProtocolTarget.TargetKind.SealEdge;target.index=i;targets.Add(target);
            var canvasGo=new GameObject("Edge Progress Ring "+(i+1),typeof(RectTransform),typeof(Canvas));Undo.RegisterCreatedObjectUndo(canvasGo,"Build edge ring");canvasGo.transform.SetParent(edges.transform,true);canvasGo.transform.position=pos+Vector3.up*.016f;canvasGo.transform.rotation=Quaternion.Euler(90,0,0);canvasGo.transform.localScale=Vector3.one*.0003f;canvasGo.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
            var img=flow.panel.Box("Ring",canvasGo.transform,Vector2.zero,new Vector2(60,60),new Color(.3f,1,.7f));img.sprite=ring;img.type=Image.Type.Filled;img.fillMethod=Image.FillMethod.Radial360;img.fillAmount=0;flow.edgeRings[i]=img;
        }
        var tray=Primitive("PLACEHOLDER Next Team Handover Tray",PrimitiveType.Cube,flow.transform,new Vector3(-1.15f,.85f,.73f),new Vector3(.18f,.015f,.22f),teal,true);
        flow.handoverSocket=Socket("Next Team Plate Socket",tray.transform,new Vector3(-1.15f,.88f,.73f),Quaternion.identity,.13f,PCRProtocolSocket.SocketRole.Handover);
        Label("Next Team Label",flow.transform,"NEXT TEAM",new Vector3(-1.04f,.92f,.73f),.18f);
        flow.targets=targets.ToArray();inspection.SetActive(false);edges.SetActive(false);EditorUtility.SetDirty(flow);EditorSceneManager.MarkSceneDirty(flow.gameObject.scene);AssetDatabase.SaveAssets();
    }
}
#endif
