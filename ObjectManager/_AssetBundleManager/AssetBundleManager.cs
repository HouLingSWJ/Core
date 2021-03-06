﻿using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AssetBundleManager {

    #region 常量
    public const string c_streamingAssetsPathName = "Assets/" + c_bundleTestRootPath; //bundle 的保存位置

   // private const string c_bundleTestRootPath = "Scripts/Core/ObjectManager/_AssetBundleManager/Example_AssetBundleManager/StreamingAssets";//从Assets下 到 bundle 根节点根据自己需要修改 (测试版)
    private const string c_bundleTestRootPath = "StreamingAssets";//从Assets下 到 bundle 根节点根据自己需要修改 （正式版）
    private const string c_mainBundleName = "StreamingAssets";//主bundle 名称
    private const string c_manifestName = "AssetBundleManifest";//主manifest 名称
    #endregion

    #region 变量
    private static AssetBundleManifest s_assetBundleManifest; // manifest 文件 ，记录了所有bundle的依赖关系
    private static Dictionary<string, AssetBundle> s_allLoadedBundle = new Dictionary<string, AssetBundle>();//所有已经加载的bundle
    private static Dictionary<string, int> s_allLoadedBundleUsedNum = new Dictionary<string, int>();//所有已经加载的bundle 被引用的数量

    private static Dictionary<int, List<string>> s_allAssetUsedBundle = new Dictionary<int, List<string>>();//所有asset -> bundle 引用  key是instanceID
    private static List<string> needUnLoadBundleName = new List<string>();//需要卸载的assetbundle
    #endregion

    #region 提供外部调用的方法
    //初始化
    static public void Init()
    {
        InitAssetBundleMainfest();
    }

    //卸载一个assetbundle 
    static public void UnloadOneAsset(string assetName, bool must)
    {
        RemoveAssetBundle(assetName, must);
    }

    //卸载自定义列表中的Assetbundle 
    static public void UnloadSomeAsset(List<string> assetNames, bool must)
    {
        for (int i = 0; i < assetNames.Count; i++)
        {
            RemoveAssetBundle(assetNames[i], must);
        }
    }

    //卸载所有assetBundle  unload(true)
    static public void UnloadAllAssetBundle(bool must)
    {

        foreach (var item in s_allLoadedBundle)
        {
            needUnLoadBundleName.Add(item.Key);
        }

        UnloadNeedUnLoadBundle(must);

    }


    #endregion

    #region 内部调用的方法
    //初始化 加载主manifest文件
    static private void InitAssetBundleMainfest()
    {
        if (s_assetBundleManifest != null)
        {
            return;
        }
        string l_path =
        #if UNITY_EDITOR
            Path.Combine(Application.dataPath, c_bundleTestRootPath);
        #else
            "jar:file://"+Application.dataPath + "!/assets/";
        #endif


        l_path = Path.Combine(l_path, c_mainBundleName);
        var myLoadedAssetBundle = AssetBundle.LoadFromFile(l_path);
        s_assetBundleManifest = (AssetBundleManifest)myLoadedAssetBundle.LoadAsset(c_manifestName, typeof(AssetBundleManifest));
        myLoadedAssetBundle.Unload(false);

    }

    //根据bundle 名称和 资源名  ，加载资源
    static public T Load<T>(string bundleName, string resName = null, bool isDepend = false) where T : UnityEngine.Object
    {
        Init();
        string l_path =
        #if UNITY_EDITOR
             Path.Combine(Application.dataPath, c_bundleTestRootPath);
        #else
            "jar:file://" + Application.dataPath + "!/assets/";
        #endif

        l_path = Path.Combine(l_path, bundleName);

        AssetBundle myLoadedAssetBundle = LoadOneAssetBundle(bundleName, l_path);

        if (myLoadedAssetBundle == null)
        {
            Debug.Log("Failed to load AssetBundle:" + bundleName);
            return default(T);
        }
        else
        {
            if (s_assetBundleManifest != null)
            {
                string[] dependAssets = s_assetBundleManifest.GetAllDependencies(bundleName);

                for (int i = 0; i < dependAssets.Length; i++)
                {
                    //Debug.Log("加载bundle:" + bundleName + "的依赖包：" + dependAssets[i]);
                    Load<T>(dependAssets[i], isDepend: true);
                }
            }

            if (isDepend)
            {
                return default(T);
            }
            else
            {
                T asset = myLoadedAssetBundle.LoadAsset<T>(resName);
                //Debug.Log(asset);
                //记录asset 与 bundle 之间的关系
                AssetBundleManager.RecordAssetUsedBundle(asset, bundleName);
                return asset;
            }

        }
    }


    public static AsyncOperation s_async_operation; //场景加载信息
    //根据bundle 名称和 资源名  ，加载资源
    static public IEnumerator LoadScene(string bundleName, string sceneName = null ,SceneLoadCallBack callBack = null) 
    {
        Init();
        string l_path =
#if UNITY_EDITOR
             Path.Combine(Application.dataPath, c_bundleTestRootPath);
#else
            "jar:file://" + Application.dataPath + "!/assets/";
#endif

        l_path = Path.Combine(l_path, bundleName);

        AssetBundle myLoadedAssetBundle = LoadOneAssetBundle(bundleName, l_path);

        if (myLoadedAssetBundle == null)
        {
            Debug.Log("Failed to load AssetBundle:" + bundleName);
            yield return null;
        }
        else
        {
            if (s_assetBundleManifest != null)
            {
                string[] dependAssets = s_assetBundleManifest.GetAllDependencies(bundleName);

                for (int i = 0; i < dependAssets.Length; i++)
                {
                    //Debug.Log("加载bundle:" + bundleName + "的依赖包：" + dependAssets[i]);
                    Load<Object>(dependAssets[i], isDepend: true);
                }
            }
        }
        if (callBack != null)
        {
            callBack();
        }

        s_async_operation = SceneManager.LoadSceneAsync(sceneName);
        yield return s_async_operation;
    }




    //记录 Asset 对 Bundle 使用情况
    static public void RecordAssetUsedBundle(Object obj, string abName)
    {
        int instanceID = obj.GetInstanceID();
        if (s_allAssetUsedBundle.ContainsKey(instanceID))
        {
            if (s_allAssetUsedBundle[instanceID] == null)
            {
                List<string> abNameList = new List<string>();
                s_allAssetUsedBundle[instanceID] = abNameList;

            }
            s_allAssetUsedBundle[instanceID].Add(abName);
            s_allLoadedBundleUsedNum[abName] = s_allLoadedBundleUsedNum[abName] + 1;
        }
        else
        {
            List<string> abNameList = new List<string>();
            s_allAssetUsedBundle.Add(instanceID, abNameList);
            s_allAssetUsedBundle[instanceID].Add(abName);
        }

        string[] dependAssets = s_assetBundleManifest.GetAllDependencies(abName);

        for (int i = 0; i < dependAssets.Length; i++)
        {
            Debug.Log("记录bundle： :" + abName + "的依赖包：" + dependAssets[i]);
            RecordAssetUsedBundle(obj, dependAssets[i]);
        }
    }

    //清除 Asset 对 Bundle 的使用标记
    static public void RemoveAssetUsedBundle(Object asset, string abName)
    {

        int instanceID = asset.GetInstanceID();
        if (s_allAssetUsedBundle.ContainsKey(instanceID))
        {
            s_allLoadedBundleUsedNum[abName] = s_allLoadedBundleUsedNum[abName] - 1;  // abName 这个bundle的被引用数减一
            s_allAssetUsedBundle[instanceID].Remove(abName); // asset 这个asset不再使用 abName 这个bundle
        }
        else
        {
            Debug.Log("资源：" + asset + " ID: " + instanceID + "不在s_allAssetsUsedBundle记录中！ ");
        }

        string[] dependAssets = s_assetBundleManifest.GetAllDependencies(abName);

        for (int i = 0; i < dependAssets.Length; i++)
        {
            //Debug.Log("清除bundle： :" + abName + "的依赖包：" + dependAssets[i]);
            RemoveAssetUsedBundle(asset, dependAssets[i]);
        }
    }

#endregion

#region 提供编辑器调用的方法

    //获取当前所有资源对AssetBundle 的使用情况  (asset -> bundle)
    public static Dictionary<int, List<string>> GetAllAssetUsedBundle()
    {
        //foreach (var item in s_allLoadedBundleUsedNum)
        //{
        //    Debug.Log(s_allLoadedBundle[item.Key].GetInstanceID().ToString() + s_allLoadedBundle[item.Key]);
        //}
        return new Dictionary<int, List<string>>(s_allAssetUsedBundle);
    }

    //通过Asset名 获取Asset
    public static Object GetAssetByName(string assetName)
    {
        return s_allLoadedBundle[assetName];
    }

#endregion

#region 内部工具方法

    //加载一个AsssetBundle, 并记录
    static private AssetBundle LoadOneAssetBundle(string name,string path)
    {
        AssetBundle myAssetBundle = null;
        if (s_allLoadedBundle.ContainsKey(name))
        {
            myAssetBundle = s_allLoadedBundle[name];
            s_allLoadedBundleUsedNum[name] = s_allLoadedBundleUsedNum[name] + 1; //引用数量+1
            //Debug.Log("读取已解压的bundle" + name);
        }
        else
        {
            myAssetBundle = AssetBundle.LoadFromFile(path);

            s_allLoadedBundle.Add(name, myAssetBundle); //添加为已加载
            s_allLoadedBundleUsedNum.Add(name, 1); //引用数量 0
            //Debug.Log("加载新引用  " + name + s_allLoadedBundleUsedNum[name]);
        }
        return myAssetBundle;
    }

    //释放一个bundle 。must == true时表示如果还有引用 , 则使用unload（false），会产生游离asset，游离asset 由  assetManager进行 管理、卸载 
    static private void RemoveAssetBundle(string bundleName,bool must)
    {
        if (s_allLoadedBundle.ContainsKey(bundleName))
        {
            if (s_allLoadedBundleUsedNum[bundleName] < 1)
            {
                Debug.LogWarning("正常卸载Assetbundle: " + bundleName );
                RemoveBundleDataAndUnload(bundleName, true);
            }
            else if (must)
            {
                Debug.LogWarning("强制卸载Assetbundle: " + bundleName +" 其引用数量为" + s_allLoadedBundleUsedNum[bundleName]);
                RemoveBundleDataAndUnload(bundleName, false);
            }
            else
            {
                Debug.LogWarning("不能卸载Assetbundle: " + bundleName + "!  因为其引用数量为" + s_allLoadedBundleUsedNum[bundleName]);
            }
        }
        else
        {
            Debug.LogError("AssetBundle : " + bundleName + "  不存在已加载列表中，无法卸载！");
        }
    }

    //移除bundle 的记录和加载记录
    static private void RemoveBundleDataAndUnload(string bundleName,bool unloadAllLoadedObjects)
    {

        //清除 asset -> bundle  的记录
        foreach (var item in s_allAssetUsedBundle)
        {
            if (item.Value.Contains(bundleName))
            {
                AssetManager.SetAssetFree(item.Key);
                item.Value.Remove(bundleName);
            }
        }
        Debug.Log("卸载 assetBundle： " + s_allLoadedBundle[bundleName].GetInstanceID());

        s_allLoadedBundle[bundleName].Unload(unloadAllLoadedObjects);
        s_allLoadedBundle.Remove(bundleName);
        s_allLoadedBundleUsedNum.Remove(bundleName);
    }

    //移除卸载列表中的所有bundle
    static private void UnloadNeedUnLoadBundle(bool unloadAllLoadedObjects)
    {
        for (int i = 0; i < needUnLoadBundleName.Count; i++)
        {
            RemoveAssetBundle(needUnLoadBundleName[i], unloadAllLoadedObjects);
        }
        needUnLoadBundleName.Clear();
    }


#endregion
}

public delegate void SceneLoadCallBack();

