using System.Linq;
using TMPro;
using TowerOfBabel;
using TowerOfBabel.Interface;
using TowerOfBabel.Resources;
using TowerOfBabel.Upgrades;
using UnityEditor;
using UnityEngine;

/// <summary>Temporary Edit Mode visual inspection. Never saves sample rows into the game scene.</summary>
public static class FrutigerAeroPreview
{
    public static void Show(string screen)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Edit Mode preview only.");
        Clear();
        var root=GameObject.Find("CanvasInterfaceManager");
        var visuals=root.transform.Find(screen+"/Visuals"); visuals.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        if(screen=="Upgrade")
        {
            var config=AssetDatabase.LoadAssetAtPath<UpgradeTreeConfig>("Assets/_Project/Resources/UpgradeTreeConfig.asset");
            var board=(RectTransform)visuals.Find("BoardWell/Viewport/GatherBoard");
            Vector2 size=UpgradeBoardLayout.NodeSize;
            foreach(var data in config.GetUpgrades(UpgradeJob.Gather))
            {
                var go=Spawn("Assets/_Project/Prefabs/Interface/UpgradeButton.prefab",board);
                var view=go.GetComponent<UpgradeButton>(); view.Configure(data,null,UpgradeButtonState.CanBuy);
                go.SetActive(!data.IsLevelFiftyUpgrade && data.Row==3 && data.Column==3);
                view.BindTooltip(visuals.Find("UpgradeTooltip").GetComponent<UpgradeTooltip>());
                var rect=(RectTransform)go.transform; rect.anchorMin=rect.anchorMax=rect.pivot=new(.5f,.5f);
                rect.sizeDelta=size; rect.anchoredPosition=UpgradeBoardLayout.TreePosition(data);
            }
        }
        if(screen=="Upgrade") visuals.Find("BoardWell/Viewport/GatherBoard").GetComponent<AeroTreeConnections>()?.Refresh();
        if(screen=="Inventory")
        {
            var definitions=AssetDatabase.LoadAssetAtPath<ResourceDefinitionCollection>("Assets/_Project/Resources/Definitions/ResourceDefinitions.asset");
            var content=visuals.Find("ResourceScroll/Viewport/Resources");
            foreach(var definition in definitions.Definitions.Where(d=>d!=null))
                Spawn("Assets/_Project/Prefabs/Interface/ResourceInventory.prefab",content).GetComponent<ResourceInventoryDisplay>().Bind(definition,0);
        }
        if(screen=="AnimationEmote")
        {
            var content=visuals.Find("Options");
            string[] labels={"Dance","Hip Hop 1","Hip Hop 2","Hip Hop 3","Rumba","Silly Dance"};
            for(int i=0;i<labels.Length;i++)
            {
                var go=Spawn("Assets/_Project/Prefabs/Interface/OptionButton.prefab",content);
                go.GetComponent<OptionButton>().Configure(labels[i],null);
                float radians=(90-i*60)*Mathf.Deg2Rad;
                ((RectTransform)go.transform).anchoredPosition=new Vector2(Mathf.Cos(radians),Mathf.Sin(radians))*130;
            }
        }
        Canvas.ForceUpdateCanvases(); EditorApplication.QueuePlayerLoopUpdate();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }
    public static void Clear()
    {
        var root=GameObject.Find("CanvasInterfaceManager"); if(root==null) return;
        foreach(var transform in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("_AeroPreview_")).ToArray())
            if(transform!=null) Object.DestroyImmediate(transform.gameObject);
        foreach(string name in new[]{"Upgrade","Inventory","AnimationEmote","Interaction"})
            root.transform.Find(name+"/Visuals").gameObject.SetActive(false);
    }
    private static GameObject Spawn(string path,Transform parent)
    {
        var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),parent);
        go.name="_AeroPreview_"+go.name; go.hideFlags=HideFlags.DontSaveInEditor; return go;
    }

}
