using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Management;
using Google.XR.ARCoreExtensions;

public class L4SessionExit : MonoBehaviour
{
    [SerializeField] ARSession session;
    [SerializeField] ARCameraManager cameraManager;
    [SerializeField] ARCameraBackground cameraBackground;
    [SerializeField] ARFaceManager faceManager;
    [SerializeField] ARRuntimeImageLibrary imageLibrary;
    [SerializeField] ARCoreExtensions extensions;
    bool exiting;

    public void Back()
    {
        if (!exiting)
        {
            exiting = true;
            StartCoroutine(Exit());
        }
    }

    IEnumerator Exit()
    {
        // Back must finish image jobs before pausing/destroying the native AR session.
        if (imageLibrary != null)
            imageLibrary.PrepareForSceneExit();
        if (faceManager != null)
            faceManager.enabled = false;
        if (cameraBackground != null)
            cameraBackground.enabled = false;
        if (cameraManager != null)
            cameraManager.enabled = false;
        if (extensions != null)
            extensions.enabled = false;
        if (session != null)
            session.enabled = false;

        yield return null;
        // ARCore image databases release native references from their finalizers.
        // Release unreferenced databases while their owning native session still exists.
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();

        var manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        if (manager != null)
        {
            // A rear/front camera change must not resume the previous native camera session.
            manager.StopSubsystems();
            manager.DeinitializeLoader();
            yield return null;
            yield return manager.InitializeLoader();
            if (manager.activeLoader == null)
            {
                Debug.LogError("[Lab4] Reinitializarea XR a esuat; nu incarc o alta scena AR.");
                exiting = false;
                yield break;
            }
            manager.StartSubsystems();
        }
        Debug.Log("[Lab4] Sesiune XR reinitializata inainte de revenirea in meniu.");
        SceneManager.LoadScene(0);
    }
}
