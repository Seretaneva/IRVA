using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(ARRaycastManager))]
public class CloudAnchorObjectPlacement : MonoBehaviour
{
    /// <summary>
    /// The object instantiated as a result of a successful raycast intersection with a plane.
    /// </summary>
    public GameObject spawnedObject { get; private set; }

    /// <summary>
    /// The first-person camera being used to render the passthrough camera image (i.e. AR
    /// background).
    /// </summary>
    public Camera FirstPersonCamera;

    /// <summary>
    /// A prefab to place an object when a raycast from a user touch hits a plane.
    /// </summary>
    public GameObject prefab;

    public static CloudAnchorObjectPlacement Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        else
        {
            Instance = this;
        }

        m_RaycastManager = GetComponent<ARRaycastManager>();
        spawnedObject = null;
        planeManager = GetComponent<ARPlaneManager>();
    }

    Transform placementAnchor;
    ARPlaneManager planeManager;
    bool placing;
    int placementGeneration;

    bool TryGetTouchPosition(out Vector2 touchPosition)
    {
        touchPosition = default;

        var screen = Touchscreen.current;

        if (screen == null ||
            !screen.primaryTouch.press.wasPressedThisFrame)
        {
            return false;
        }

        Vector2 position = screen.primaryTouch.position.ReadValue();

        // Verifică dacă atingerea este peste interfața UI.
        if (EventSystem.current != null)
        {
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = position
            };

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, results);

            if (results.Count > 0)
            {
                return false;
            }
        }

        touchPosition = position;
        return true;
    }

    // Update is called once per frame
    async void Update()
    {
        /* Add only one cube on scene */
        if (placing || spawnedObject != null || ARSession.state != ARSessionState.SessionTracking ||
            ARCloudAnchorManager.Instance == null || ARCloudAnchorManager.Instance.IsBusy)
            return;

        if (!TryGetTouchPosition(out Vector2 touchPosition))
            return;
        Debug.Log("Touch detected.");
        if (m_RaycastManager.Raycast(touchPosition, s_Hits, TrackableType.PlaneWithinPolygon))
        {
            Debug.Log("Plan atins.");
            var hitPose = s_Hits[0].pose;

            /* TODO 3.1. Instantiate a new prefab on scene */
            spawnedObject = Instantiate(prefab,hitPose.position,hitPose.rotation);
            spawnedObject.SetActive(false);
            placing = true;
            int generation = placementGeneration;

            /* TODO 3.2 Attach an anchor to the prefab */
            ARAnchor anchor;
            try
            {
                var result = await GetComponent<ARAnchorManager>().TryAddAnchorAsync(hitPose);
                anchor = result.status.IsSuccess() ? result.value : null;
                if (this == null || generation != placementGeneration || !isActiveAndEnabled)
                {
                    if (anchor != null) Destroy(anchor.gameObject);
                    return;
                }
                placing = false;
                if (anchor == null)
                {
                    ClearPlacement();
                    ARCloudAnchorManager.Instance.PlacementFailed("Crearea ancorei locale a esuat: " + result.status + ". Asteapta tracking stabil, apoi atinge din nou planul.");
                    return;
                }
            }
            catch (System.Exception exception)
            {
                if (this == null || generation != placementGeneration || !isActiveAndEnabled) return;
                placing = false;
                ClearPlacement();
                ARCloudAnchorManager.Instance.PlacementFailed("Crearea ancorei locale a esuat: " + exception.Message);
                return;
            }
            placementAnchor = anchor.transform;
            spawnedObject.transform.SetParent(placementAnchor, false);
            spawnedObject.transform.localPosition = Vector3.zero;
            spawnedObject.transform.localRotation = Quaternion.identity;
            
            /* Send the anchor to ARCloudAnchorManager */
            ARCloudAnchorManager.Instance.QueueAnchor(anchor);

        }
    }

    /* Add the object on scene after the anchor has been resolved */
    public void RecreatePlacement(Transform transform)
    {
        ClearPlacement();
        placementAnchor = transform;
        spawnedObject = Instantiate(prefab, transform.position, transform.rotation);
        spawnedObject.transform.SetParent(transform, false);
        spawnedObject.transform.localPosition = Vector3.zero;
        spawnedObject.transform.localRotation = Quaternion.identity;
    }

    public void RemovePlacement()
    {
        /* TODO 5 Remove the cube from screen */
        if (ARCloudAnchorManager.Instance != null && ARCloudAnchorManager.Instance.IsBusy) return;
        placementGeneration++;
        placing = false;
        ClearPlacement();
        if (ARCloudAnchorManager.Instance != null) ARCloudAnchorManager.Instance.QueueAnchor(null);
    }

    private void ClearPlacement()
    {
        if (spawnedObject != null) spawnedObject.SetActive(false);
        Destroy(spawnedObject);
        if (placementAnchor != null) Destroy(placementAnchor.gameObject);
        spawnedObject = null;
        placementAnchor = null;
    }

    private void LateUpdate()
    {
        if (spawnedObject != null && placementAnchor != null)
        {
            var localAnchor = placementAnchor.GetComponent<ARAnchor>();
            var cloudAnchor = placementAnchor.GetComponent<Google.XR.ARCoreExtensions.ARCloudAnchor>();
            bool tracked = ARSession.state == ARSessionState.SessionTracking &&
                ((localAnchor != null && localAnchor.trackingState == TrackingState.Tracking) ||
                 (cloudAnchor != null && cloudAnchor.trackingState == TrackingState.Tracking));
            if (spawnedObject.activeSelf != tracked)
            {
                spawnedObject.SetActive(tracked);
                Debug.Log("[Lab3] Content visible=" + tracked + "; session=" + ARSession.state + "; reason=" + ARSession.notTrackingReason);
            }
        }
        // Hide only plane rendering once placed; detection and anchors keep running independently.
        if (planeManager == null) return;
        foreach (var plane in planeManager.trackables)
        {
            bool visible = spawnedObject == null && plane.subsumedBy == null && plane.trackingState == TrackingState.Tracking;
            foreach (var renderer in plane.GetComponentsInChildren<Renderer>(true)) renderer.enabled = visible;
        }
    }

    private void OnDisable()
    {
        placementGeneration++;
        placing = false;
        ClearPlacement();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    static List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();
    ARRaycastManager m_RaycastManager;
}
