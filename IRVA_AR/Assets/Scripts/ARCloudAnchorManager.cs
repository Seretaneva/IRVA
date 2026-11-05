using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;
using Google.XR.ARCoreExtensions;
using TMPro;
using Unity.VisualScripting;
using Unity.XR.CoreUtils;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.SceneManagement;

public class AnchorCreatedEvent : UnityEvent<Transform> { }

/* TODO 1. Add and configure the ARCore Extensions game object */
/* TODO 2. Enable ARCore Cloud Anchors API on Google Cloud Platform */
public class ARCloudAnchorManager : MonoBehaviour
{
    [SerializeField]
    private Camera arCamera = null;

    [SerializeField]
    TMP_Text statusUpdate;

    private ARAnchorManager  arAnchorManager = null;
    private ARAnchor pendingHostAnchor = null;
    private string anchorIdToResolve;
    private AnchorCreatedEvent anchorCreatedEvent = null;
    public static ARCloudAnchorManager Instance { get; private set; }
    public GameObject middle;
    public GameObject main;
    private const string SavedAnchorKey = "Lab3.CloudAnchorId";
    private HostCloudAnchorPromise hostPromise;
    private ResolveCloudAnchorPromise resolvePromise;
    private bool busy;
    private bool showQuality;
    private bool exiting;
    private float nextQualityUpdate;
    private XROrigin origin;
    [SerializeField, Range(1, 365)] private int lifetimeDays = 365;
    public bool IsBusy => busy;

    private void SetStatus(string text)
    {
        if (statusUpdate != null) statusUpdate.text = text;
        Debug.Log("[Lab3] " + text);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        else
        {
            Instance = this;
            arAnchorManager = GetComponent<ARAnchorManager>();
        }

        origin = GetComponent<XROrigin>();
        anchorIdToResolve = PlayerPrefs.GetString(SavedAnchorKey, "");
        anchorCreatedEvent = new AnchorCreatedEvent();
        anchorCreatedEvent.AddListener((t) => CloudAnchorObjectPlacement.Instance.RecreatePlacement(t));
        SetStatus(string.IsNullOrEmpty(anchorIdToResolve) ? "Atinge un plan pentru plasare, apoi scaneaza zona cubului." : "ID Cloud salvat disponibil. Resolve in zona scanata anterior, fara o noua plasare locala.");
    }

    public void QueueAnchor(ARAnchor arAnchor)
    {
        pendingHostAnchor = arAnchor;
        showQuality = arAnchor != null;
        if (arAnchor != null)
            Debug.Log("[Lab3] Local anchor ready; pending=" + arAnchor.pending + "; native=" + (arAnchor.nativePtr != System.IntPtr.Zero) + "; tracking=" + arAnchor.trackingState);
        if (arAnchor == null)
            SetStatus(string.IsNullOrEmpty(anchorIdToResolve) ? "Atinge un plan pentru plasare." : "Plasare eliminata. Resolve foloseste ID-ul salvat; pentru test independent: Back si reintra.");
    }

    public void PlacementFailed(string message)
    {
        pendingHostAnchor = null;
        showQuality = false;
        SetStatus(message);
    }

    public IEnumerator DisplayStatus(string text)
    {
        SetStatus(text);
        yield break;
    }

    private bool SessionReady()
    {
        if (ARSession.state == ARSessionState.SessionTracking && arAnchorManager != null &&
            arAnchorManager.subsystem != null && arAnchorManager.subsystem.running && arCamera != null && origin != null)
            return true;
        SetStatus("Tracking AR indisponibil (" + ARSession.state + ", " + ARSession.notTrackingReason + "). Cubul este ascuns temporar. Revino lent la zona scanata, cu lumina si detalii stabile.");
        return false;
    }

    private Pose CameraSessionPose()
    {
        // Native ARCore poses are session-relative, not Unity world poses with XR Origin offsets.
        Transform parent = origin.TrackablesParent;
        return new Pose(parent.InverseTransformPoint(arCamera.transform.position),
            Quaternion.Inverse(parent.rotation) * arCamera.transform.rotation);
    }

    private void Update()
    {
        if (!showQuality || busy || Time.unscaledTime < nextQualityUpdate) return;
        nextQualityUpdate = Time.unscaledTime + 1f;
        if (!SessionReady()) return;
        var quality = arAnchorManager.EstimateFeatureMapQualityForHosting(CameraSessionPose());
        SetStatus("Calitate: " + quality + ". Scaneaza lent zona cubului din mai multe unghiuri; evita suprafetele uniforme si reflexiile.");
    }

    public void Host()
    {
        if (busy || !SessionReady()) return;
        if (pendingHostAnchor == null || pendingHostAnchor.nativePtr == System.IntPtr.Zero ||
            pendingHostAnchor.trackingState != TrackingState.Tracking)
        {
            SetStatus(pendingHostAnchor == null ? "Nu exista ancora locala confirmata. Atinge un plan cu tracking stabil." :
                "Ancora locala nu este pregatita: native=" + (pendingHostAnchor.nativePtr != System.IntPtr.Zero) + "; tracking=" + pendingHostAnchor.trackingState + ". Asteapta recuperarea tracking-ului.");
            return;
        }
        /* TODO 4.1 Get FeatureMapQuality */
        Pose pose = CameraSessionPose();
        FeatureMapQuality quality = arAnchorManager.EstimateFeatureMapQualityForHosting(pose);
        StartCoroutine(DisplayStatus("Host in curs. Calitate reala: " + quality +
            (quality == FeatureMapQuality.Insufficient ? ". Calitate scazuta: ancora poate esua sau fi greu de regasit. Continua sa scanezi lent zona din mai multe unghiuri." : "")));

        // Quality is an estimate, not a native Host precondition; only an explicit Host press starts this attempt.
        busy = true;
        showQuality = false;
        Debug.Log("[Lab3] Host start; quality=" + quality + "; TTL=" + lifetimeDays + "; cameraSession=" + pose.position + "; anchorTracking=" + pendingHostAnchor.trackingState);
        /* TODO 4.2 Start the hosting process */
        hostPromise = arAnchorManager.HostCloudAnchorAsync(pendingHostAnchor, lifetimeDays);

        /* Wait for the promise to solve (Hint! Pass the HostCloudAnchorPromise variable to the coroutine) */
        StartCoroutine(WaitHostingResult(hostPromise));
    }

    public void Resolve()
    {
        if (busy || !SessionReady()) return;
        if (CloudAnchorObjectPlacement.Instance.spawnedObject != null)
        {
            SetStatus("Elimina mai intai plasarea locala cu Delete; pentru a demonstra persistenta, Back si reintra in L3.");
            return;
        }
        if (string.IsNullOrEmpty(anchorIdToResolve))
        {
            SetStatus("Nu exista ID salvat. Finalizeaza mai intai Host cu succes.");
            return;
        }
        busy = true;
        showQuality = false;
        StartCoroutine(DisplayStatus("Resolve call in progress"));

        /* TODO 6 Start the resolve process and wait for the promise */
        resolvePromise = arAnchorManager.ResolveCloudAnchorAsync(anchorIdToResolve);
        StartCoroutine(WaitResolvingResult(resolvePromise));

    }

    private IEnumerator WaitHostingResult(HostCloudAnchorPromise hostingPromise)
    {
        /* TODO 4.3 Wait for the promise. Save the id if the hosting succeeded */
        float deadline = Time.realtimeSinceStartup + 180f;
        while (hostingPromise.State == PromiseState.Pending && Time.realtimeSinceStartup < deadline)
            yield return null;
        if (hostingPromise.State == PromiseState.Pending)
        {
            hostingPromise.Cancel();
            SetStatus("Host: timeout dupa 180 secunde. Verifica internetul si configurarea Google Cloud.");
        }
        else if (hostingPromise.State != PromiseState.Cancelled)
        {
            var result = hostingPromise.Result;
            if (result.CloudAnchorState == CloudAnchorState.Success && !string.IsNullOrEmpty(result.CloudAnchorId))
            {
                anchorIdToResolve = result.CloudAnchorId;
                PlayerPrefs.SetString(SavedAnchorKey, anchorIdToResolve);
                PlayerPrefs.Save();
                SetStatus("Host Success. ID salvat pe dispozitiv. Delete elimina ancora locala; Back si reintra, apoi Resolve in aceeasi zona.");
            }
            else
            {
                SetStatus("Host: " + result.CloudAnchorState + ". " + ExplainCloudError(result.CloudAnchorState));
            }
        }
        hostPromise = null;
        busy = false;
    }

    private IEnumerator WaitResolvingResult(ResolveCloudAnchorPromise resolvePromise)
    {
        float deadline = Time.realtimeSinceStartup + 180f;
        while (resolvePromise.State == PromiseState.Pending && Time.realtimeSinceStartup < deadline)
            yield return null;
        if (resolvePromise.State == PromiseState.Pending)
        {
            resolvePromise.Cancel();
            SetStatus("Resolve: timeout dupa 180 secunde. Revino in zona scanata la Host, cu lumina si detalii similare.");
        }
        else if (resolvePromise.State != PromiseState.Cancelled)
        {
            var result = resolvePromise.Result;
            if (result.CloudAnchorState == CloudAnchorState.Success && result.Anchor != null)
            {
                anchorCreatedEvent?.Invoke(result.Anchor.transform);
                StartCoroutine(DisplayStatus("Anchor resolved successfully!"));
            }
            else
            {
                StartCoroutine(DisplayStatus("Resolve: " + result.CloudAnchorState + ". " + ExplainCloudError(result.CloudAnchorState)));
            }
        }
        this.resolvePromise = null;
        busy = false;
    }

    private static string ExplainCloudError(CloudAnchorState state)
    {
        switch (state)
        {
            case CloudAnchorState.ErrorHostingDatasetProcessingFailed:
                return "Datele vizuale nu au putut fi procesate. Scaneaza obiecte fixe cu detalii distincte, din mai multe pozitii, in lumina buna; apoi reincearca Host. Nu s-a salvat un ID nou.";
            case CloudAnchorState.ErrorNotAuthorized:
                return "Verifica ARCore API si clientul OAuth Android pentru pachetul aplicatiei si SHA-1 al certificatului APK instalat.";
            case CloudAnchorState.ErrorResourceExhausted:
                return "Cota ARCore API a fost depasita. Verifica limitele proiectului Google Cloud.";
            case CloudAnchorState.ErrorResolvingCloudIdNotFound:
                return "ID-ul Cloud nu a fost gasit sau a expirat. Este necesar un Host nou, confirmat cu Success.";
            case CloudAnchorState.ErrorInternal:
                return "Eroare interna ARCore. Back si reintra intr-o sesiune AR noua inainte de o noua incercare.";
            default:
                return "Verifica starea tracking-ului, conexiunea si logurile pentru acest rezultat ARCore.";
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        CancelOperations();
    }

    private void CancelOperations()
    {
        // Cancel native futures while this scene's AR session is still alive, not only the coroutines.
        if (hostPromise != null && hostPromise.State == PromiseState.Pending) hostPromise.Cancel();
        if (resolvePromise != null)
        {
            if (resolvePromise.State == PromiseState.Pending) resolvePromise.Cancel();
            else if (resolvePromise.State == PromiseState.Done && resolvePromise.Result.Anchor != null &&
                     CloudAnchorObjectPlacement.Instance != null && CloudAnchorObjectPlacement.Instance.spawnedObject == null)
                Destroy(resolvePromise.Result.Anchor.gameObject);
        }
        hostPromise = null;
        resolvePromise = null;
        pendingHostAnchor = null;
        busy = false;
        showQuality = false;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        anchorCreatedEvent?.RemoveAllListeners();
    }

    public void Back()
    {
        if (!isActiveAndEnabled || exiting) return;
        exiting = true;
        StopAllCoroutines();
        CancelOperations();
        busy = true;
        StartCoroutine(ExitScene());
    }

    private IEnumerator ExitScene()
    {
        // Native operations and local content must be released before resetting this session.
        if (CloudAnchorObjectPlacement.Instance != null) CloudAnchorObjectPlacement.Instance.enabled = false;
        yield return null;
        var session = FindFirstObjectByType<ARSession>();
        if (session != null) session.Reset();
        Debug.Log("[Lab3] AR session reset; local content removed, saved Cloud ID retained.");
        yield return null;
        enabled = false;
        SceneManager.LoadScene(0);
    }
}
