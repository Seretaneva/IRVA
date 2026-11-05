using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.InputSystem;
using TMPro;


public class MeasureDistances : MonoBehaviour
{
    public GameObject spawnedObject { get; private set; }

    public Camera FirstPersonCamera;

    public GameObject pointPrefab;
    public GameObject linePrefab;
    public TMP_Text textPrefab;

    public Canvas parent;
    public TMP_Text totalDistance;

    public List<GameObject> points = new List<GameObject>();
    public List<TMP_Text> distances = new List<TMP_Text>();

    float distanceSum = 0;

    GameObject selectedPoint;
    List<GameObject> lines = new List<GameObject>();

    static List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();
    ARRaycastManager m_RaycastManager;

    void Awake()
    {
        m_RaycastManager = GetComponent<ARRaycastManager>();
    }

    void Start()
    {
        if (FirstPersonCamera == null)
        {
            FirstPersonCamera = Camera.main;
        }

        if (FirstPersonCamera == null ||
            pointPrefab == null ||
            linePrefab == null ||
            textPrefab == null ||
            parent == null ||
            totalDistance == null)
        {
            Debug.LogError(
                "Completeaza referintele scriptului MeasureDistances in Inspector."
            );

            enabled = false;
            return;
        }

        totalDistance.text = "Total distance:\n0.00 m";
    }

    bool TryGetTouchPosition(out Vector2 touchPosition)
    {
        touchPosition = default;

        if (Touchscreen.current == null)
            return false;

        var touch = Touchscreen.current.primaryTouch;

        if (!touch.press.wasPressedThisFrame)
            return false;

        touchPosition = touch.position.ReadValue();
        return true;
    }

    void Update()
    {
        if (!TryGetTouchPosition(out Vector2 touchPosition))
            return;

        // Selectarea unui cub existent.
        Ray ray = FirstPersonCamera.ScreenPointToRay(touchPosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Debug.Log("Am atins: " + hit.collider.name);
            foreach (GameObject point in points)
            {
                // Collider-ul poate fi pe obiectul principal
                // sau pe copilul Cube.
                if (hit.collider.transform == point.transform ||
                    hit.collider.transform.IsChildOf(point.transform))
                {
                    selectedPoint = point;

                    Debug.Log(
                        "Cub selectat. Atinge un plan pentru a-l muta."
                    );

                    return;
                }
            }
        }

        // Cautarea unui plan AR la pozitia atinsa.
        Debug.Log("Cubul nu a fost selectat.");
        if (!m_RaycastManager.Raycast(
                touchPosition,
                s_Hits,
                TrackableType.PlaneWithinPolygon))
        {
            return;
        }

        Pose hitPose = s_Hits[0].pose;

        // Daca exista un cub selectat, il mutam.
        if (selectedPoint != null)
        {
            selectedPoint.transform.position = hitPose.position;
            selectedPoint = null;

            return;
        }

        // Altfel, adaugam un cub nou.
        spawnedObject = Instantiate(
            pointPrefab,
            hitPose.position,
            hitPose.rotation
        );

        points.Add(spawnedObject);

        if (points.Count > 1)
        {
            GameObject line = Instantiate(linePrefab);
            lines.Add(line);

            TMP_Text partialDistance = Instantiate(
                textPrefab,
                parent.transform,
                false
            );

            distances.Add(partialDistance);
        }
    }

    void LateUpdate()
    {
        // Actualizam dupa mutare si cand se misca telefonul.
        RecalculateMeasurements();
    }

    void RecalculateMeasurements()
    {
        distanceSum = 0;

        for (int i = 0; i < lines.Count; i++)
        {
            Vector3 primul = points[i].transform.position;
            Vector3 urmatorul = points[i + 1].transform.position;

            Vector3 mijloc = (primul + urmatorul) / 2f;
            float distance = Vector3.Distance(primul, urmatorul);

            // Linia este centrata intre cuburi.
            lines[i].transform.position = mijloc;

            if (distance > 0.0001f)
            {
                lines[i].transform.LookAt(urmatorul);
            }

            
            lines[i].transform.localScale =
                new Vector3(0.1f, 0.1f, distance/0.05f);

            distances[i].text = distance.ToString("F2") + " m";

            // Pozitionarea textului pe Canvas.
            Vector3 screenPosition =
                FirstPersonCamera.WorldToScreenPoint(mijloc);

            bool esteInFataCamerei = screenPosition.z > 0;
            distances[i].gameObject.SetActive(esteInFataCamerei);

            if (esteInFataCamerei)
            {
                distances[i].transform.position = new Vector3(
                    screenPosition.x,
                    screenPosition.y + 20f,
                    0f
                );
            }

            distanceSum += distance;
        }

        totalDistance.text =
            "Total distance:\n" + distanceSum.ToString("F2") + " m";
    }
}