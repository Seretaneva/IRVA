using System.Collections;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TMPro;

/* TODO 1 Create in Unity an image database with minimum 3 images */
/* TODO 2 Augment the database */
/* TODO 4 Detect and augment human faces */
public class ARRuntimeImageLibrary : MonoBehaviour
{
    private ARTrackedImageManager trackImageManager;
    public GameObject m_PlacedPrefab;
    [SerializeField] XRReferenceImageLibrary staticLibrary;
    [SerializeField] TMP_Text modeButtonText;
    [SerializeField] string runtimeImageUrl = "https://raw.githubusercontent.com/opencv/opencv/4.x/samples/data/graf1.png";
    [SerializeField, Min(0.001f)] float runtimeImageWidth = 0.20f;

    readonly Dictionary<System.Guid, GameObject> augmentations = new Dictionary<System.Guid, GameObject>();
    readonly Dictionary<TrackableId, System.Guid> associations = new Dictionary<TrackableId, System.Guid>();
    readonly System.Guid runtimeAugmentationId = System.Guid.NewGuid();
    readonly HashSet<System.Guid> activeImages = new HashSet<System.Guid>();
    UnityWebRequest activeRequest;
    Texture2D downloadedTexture;
    RuntimeReferenceImageLibrary pendingLibrary;
    RuntimeReferenceImageLibrary staticRuntimeLibrary;
    RuntimeReferenceImageLibrary runtimeLibrary;
    bool preparingStatic;
    JobHandle pendingJob;
    bool jobScheduled;
    bool preparingRuntime;
    bool runtimeMode;

    void Awake()
    {
        trackImageManager = GetComponent<ARTrackedImageManager>();
        if (staticLibrary == null && trackImageManager != null)
            staticLibrary = trackImageManager.referenceLibrary as XRReferenceImageLibrary;
    }

    void OnEnable()
    {
        if (trackImageManager == null || staticLibrary == null || m_PlacedPrefab == null)
        {
            Debug.LogError("[Lab4] Lipsesc managerul, biblioteca statica sau prefab-ul augmentarii.", this);
            enabled = false;
            return;
        }

        // Only this script owns visual content; the manager creates plain trackables.
        trackImageManager.trackedImagePrefab = null;
        trackImageManager.trackablesChanged.AddListener(OnTrackedImagesChanged);
        UseStaticLibrary();
    }

    void OnDisable()
    {
        if (trackImageManager != null)
        {
            trackImageManager.trackablesChanged.RemoveListener(OnTrackedImagesChanged);
            trackImageManager.enabled = false;
        }

        if (activeRequest != null)
            activeRequest.Abort();
        StopAllCoroutines();
        ReleaseDownloadResources();
        preparingRuntime = false;
        preparingStatic = false;
        runtimeMode = false;
        activeImages.Clear();
        ClearAugmentations();
    }

    public void PrepareForSceneExit()
    {
        // Finish native jobs while the session still exists, before XR loader teardown.
        enabled = false;
        ReleaseDownloadResources();
        if (trackImageManager != null && trackImageManager.subsystem != null)
        {
            trackImageManager.subsystem.Stop();
            trackImageManager.subsystem.imageLibrary = null;
        }
        staticRuntimeLibrary = null;
        runtimeLibrary = null;
    }

    public void UseStaticLibrary()
    {
        if (!isActiveAndEnabled || preparingRuntime || preparingStatic || staticLibrary == null)
            return;

        if (staticRuntimeLibrary == null)
        {
            preparingStatic = true;
            trackImageManager.enabled = false;
            StartCoroutine(InitializeStaticLibrary());
            return;
        }
        ActivateLibrary(staticRuntimeLibrary, false);
        SetButtonText("Static: 3 imagini\nIncarca imaginea online");
    }

    IEnumerator InitializeStaticLibrary()
    {
        SetButtonText("Se initializeaza sesiunea AR...");
        float deadline = Time.realtimeSinceStartup + 30f;
        while (ARSession.state != ARSessionState.SessionInitializing && ARSession.state != ARSessionState.SessionTracking)
        {
            if (Time.realtimeSinceStartup >= deadline)
            {
                preparingStatic = false;
                RuntimeError("Sesiunea AR nu a pornit; biblioteca statica nu a fost creata.");
                yield break;
            }
            yield return null;
        }
        try
        {
            // Native ARCore databases require a live session. Create once, after session startup.
            staticRuntimeLibrary = trackImageManager.CreateRuntimeLibrary(staticLibrary);
            ActivateLibrary(staticRuntimeLibrary, false);
            SetButtonText("Static: 3 imagini\nIncarca imaginea online");
            Debug.Log("[Lab4] Biblioteca statica activata dupa pornirea sesiunii AR.");
        }
        catch (System.Exception exception)
        {
            RuntimeError("Initializarea bibliotecii statice a esuat: " + exception.Message);
        }
        finally
        {
            preparingStatic = false;
        }
    }

    public void LoadRuntimeImage()
    {
        if (!isActiveAndEnabled || preparingRuntime || preparingStatic)
            return;
        if (runtimeMode)
        {
            UseStaticLibrary();
            return;
        }
        if (staticRuntimeLibrary == null)
        {
            UseStaticLibrary();
            return;
        }
        if (runtimeLibrary != null)
        {
            ActivateLibrary(runtimeLibrary, true);
            SetButtonText("Runtime: graf1 pregatit\nRevino la 3 imagini statice");
            return;
        }
        preparingRuntime = true;
        SetButtonText("Se descarca imaginea...");
        /* TODO 3.1 Download minimum one image from the internet */
        var url = runtimeImageUrl;
        StartCoroutine(DownloadImage(url));
    }

    /* Download and create an image database */
    IEnumerator DownloadImage(string url)
    {
        try
        {
        /* UnityWebRequest API will be used to download the image */
        var request = UnityWebRequestTexture.GetTexture(url);
        activeRequest = request;
        request.timeout = 30;
        yield return request.SendWebRequest();

        Debug.Log(
            $"[Lab4] Download: {request.result}, HTTP {request.responseCode}, {request.error}");

        if (request.result != UnityWebRequest.Result.Success)
        {
            RuntimeError($"Descarcare esuata: HTTP {request.responseCode}, {request.error}");
            yield break;
        }

        /* Downloaded image */
        Texture2D imageToAdd = DownloadHandlerTexture.GetContent(request);
        downloadedTexture = imageToAdd;
        if (imageToAdd == null || !imageToAdd.isReadable || imageToAdd.width < 1 || imageToAdd.height < 1)
        {
            RuntimeError("Raspunsul nu contine o textura valida si citibila.");
            yield break;
        }
        Debug.Log($"[Lab4] Textura: {imageToAdd.width}x{imageToAdd.height}, {imageToAdd.format}");

        float deadline = Time.realtimeSinceStartup + 30f;
        while (ARSession.state != ARSessionState.SessionInitializing && ARSession.state != ARSessionState.SessionTracking)
        {
            if (Time.realtimeSinceStartup >= deadline)
            {
                RuntimeError("Sesiunea AR nu este pregatita pentru biblioteca runtime.");
                yield break;
            }
            yield return null;
        }

        /* TODO 3.2 Destroy the previous ARTrackedImageManager component.
         * Hint! What's the difference between Destroy() and DestroyImmediate()? */
        // AR Foundation 6 supports swapping libraries on the existing manager.
        // Keep the static library active until the replacement has passed validation.
        if (trackImageManager == null)
        {
            RuntimeError("Managerul de image tracking nu mai exista.");
            yield break;
        }
            // Destroy is deferred. Wait one frame before adding the new manager.

        /* TODO 3.3 Attach a new ARTrackedImageManager component */
        // Reuse the scene's single manager rather than adding a competing subsystem owner.

        /* TODO 3.4 Create a new runtime library */
        RuntimeReferenceImageLibrary library = null;
        try
        {
            library = trackImageManager.CreateRuntimeLibrary();
            pendingLibrary = library;
        }
        catch (System.Exception exception)
        {
            RuntimeError("Crearea bibliotecii runtime a esuat: " + exception.Message);
        }

        /* TODO 3.5 Add the image to the database */
        if (!(library is MutableRuntimeReferenceImageLibrary mutableLibrary))
        {
            RuntimeError("Biblioteca nu permite adaugarea imaginilor la runtime.");
            yield break;
        }

        // Set the width in meters to match the image displayed on the monitor.
        AddReferenceImageJobState addImageJob = default;
        try
        {
            if (runtimeImageWidth <= 0 || !mutableLibrary.IsTextureFormatSupported(imageToAdd.format))
                throw new System.InvalidOperationException("Latime fizica sau format de textura neacceptat.");
            addImageJob = mutableLibrary.ScheduleAddImageWithValidationJob(
                imageToAdd, "graf1", runtimeImageWidth);
            pendingJob = addImageJob.jobHandle;
            jobScheduled = true;
        }
        catch (System.Exception exception)
        {
            RuntimeError("Adaugarea imaginii a esuat: " + exception.Message);
        }
        if (!jobScheduled)
            yield break;

        JobHandle jobHandle = addImageJob.jobHandle;
        while (!jobHandle.IsCompleted)
        {
            yield return null;
        }

        jobHandle.Complete();
        jobScheduled = false;
        Debug.Log($"[Lab4] Validation: {addImageJob.status}");

        if (addImageJob.status != AddReferenceImageJobStatus.Success)
        {
            RuntimeError("Validarea imaginii a esuat: " + addImageJob.status);
            yield break;
        }

        /* Set the maximum number of moving images */
        trackImageManager.requestedMaxNumberOfMovingImages = 3;

        /* TODO 3.6 Set the new library as the reference library */
        runtimeLibrary = library;
        ActivateLibrary(library, true);

        /* Attach the event handling */
        // Both modes use the listener registered by OnEnable.

        /* TODO 3.7 Enable the new ARTrackedImageManager component */
        trackImageManager.enabled = true;
        SetButtonText("Runtime: graf1 pregatit\nRevino la 3 imagini statice");
        Debug.Log($"[Lab4] Biblioteca runtime pregatita: {library.count} imagine, validare {addImageJob.status}.");
        }
        finally
        {
            ReleaseDownloadResources();
            preparingRuntime = false;
        }
    }

    void OnTrackedImagesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        if (!isActiveAndEnabled)
            return;
        foreach (ARTrackedImage trackedImage in eventArgs.added)
        {
            /* TODO 3.8 Instantiate a new object in scene so that it always follows the tracked image
             * Hint! Use SetParent() method */
            Debug.Log($"[Lab4] Detected: {trackedImage.referenceImage.name}");

            if (m_PlacedPrefab == null)
            {
                Debug.LogError("[Lab4] Câmpul Placed Prefab este gol.");
                continue;
            }

            UpdateAugmentation(trackedImage);
        }

        foreach (ARTrackedImage trackedImage in eventArgs.updated)
        {
            /* Handle update event */
            UpdateAugmentation(trackedImage);
        }

        foreach (var removedImage in eventArgs.removed)
        {
            /* Handle removed event */
            RemoveAugmentation(removedImage.Key);
        }
    }

    void UpdateAugmentation(ARTrackedImage trackedImage)
    {
        if (!activeImages.Contains(trackedImage.referenceImage.guid))
        {
            return;
        }

        // A limited pose must never create or move content, even on the first detection.
        if (trackedImage.trackingState != TrackingState.Tracking)
            return;

        bool isStaticImage = false;
        for (int i = 0; i < staticLibrary.count; i++)
            isStaticImage |= staticLibrary[i].guid == trackedImage.referenceImage.guid;
        System.Guid imageId = isStaticImage ? trackedImage.referenceImage.guid : runtimeAugmentationId;
        associations[trackedImage.trackableId] = imageId;

        if (!augmentations.TryGetValue(imageId, out GameObject placedObj) || placedObj == null)
        {
            // An independent world-space object survives removal of its AR trackable.
            placedObj = Instantiate(m_PlacedPrefab, trackedImage.transform.position, trackedImage.transform.rotation);
            augmentations[imageId] = placedObj;
        }
        placedObj.transform.SetParent(null, true);
        placedObj.transform.SetPositionAndRotation(trackedImage.transform.position, trackedImage.transform.rotation);
        // The prefab's Cube is 0.1 m wide and its center is 0.05 m above the image.
        // A world root scale of 0.5 gives static cubes a 0.05 m side and 0.025 m center height.
        placedObj.transform.localScale = Vector3.one * (isStaticImage ? 0.5f : 1f);
        placedObj.SetActive(true);
    }

    void RemoveAugmentation(TrackableId id)
    {
        // Removal ends only this association. The image's last valid content lives until Back.
        associations.Remove(id);
    }

    void ClearAugmentations()
    {
        foreach (GameObject placedObj in augmentations.Values)
        {
            if (placedObj != null)
            {
                placedObj.SetActive(false);
                Destroy(placedObj);
            }
        }
        augmentations.Clear();
        associations.Clear();
    }

    void ActivateLibrary(IReferenceImageLibrary library, bool useRuntime)
    {
        trackImageManager.enabled = false;
        // Library switches retain the last valid cubes; only laboratory exit clears them.
        associations.Clear();
        activeImages.Clear();
        for (int i = 0; i < library.count; i++)
            activeImages.Add(library[i].guid);
        trackImageManager.referenceLibrary = library;
        runtimeMode = useRuntime;
        trackImageManager.enabled = true;
    }

    void ReleaseDownloadResources()
    {
        // Native add jobs cannot be cancelled; finish before releasing their resources.
        if (jobScheduled)
        {
            pendingJob.Complete();
            jobScheduled = false;
        }
        System.GC.KeepAlive(pendingLibrary);
        pendingLibrary = null;
        if (activeRequest != null)
        {
            activeRequest.Dispose();
            activeRequest = null;
        }
        if (downloadedTexture != null)
        {
            Destroy(downloadedTexture);
            downloadedTexture = null;
        }
    }

    void RuntimeError(string message)
    {
        Debug.LogError("[Lab4] " + message, this);
        SetButtonText("Eroare runtime. Reincearca\nStatic: 3 imagini");
    }

    void SetButtonText(string message)
    {
        if (modeButtonText != null)
            modeButtonText.text = message;
    }
}
