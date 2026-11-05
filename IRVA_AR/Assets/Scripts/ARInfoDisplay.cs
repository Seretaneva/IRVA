using UnityEngine;
using UnityEngine.XR.ARFoundation;
using TMPro;
using UnityEngine.XR.ARSubsystems;

public class ARInfoDisplay : MonoBehaviour
{
    public ARPlaneManager aRPlaneManager;
    public TMP_Text infoText ;
    public Transform cameraTransform;
    public ARPointCloudManager aRPointCloudManager;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {   
        
        Vector3 pozitie = cameraTransform.position;
        Vector3 orientare =  cameraTransform.eulerAngles;
        int nrPlane = 0;

        foreach (var plane in aRPlaneManager.trackables)
        {
            if (plane.trackingState == TrackingState.Tracking &&
                plane.subsumedBy == null)
            {
                nrPlane++;
            }
        }
        int nrPuncte = 0;
        foreach(var cloud in aRPointCloudManager.trackables)
        {
            if(cloud.positions.HasValue)
            {
                nrPuncte += cloud.positions.Value.Length;
            }
        }
        infoText.text = "Plane " + nrPlane + "\nPozitie camera: " 
        + pozitie.ToString("F2") + "\nOrientare camera: " + orientare.ToString("F2") + "\nNr de puncte: " + nrPuncte;
    }
}
