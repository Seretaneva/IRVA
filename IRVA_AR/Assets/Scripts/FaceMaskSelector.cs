using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class FaceMaskSelector : MonoBehaviour
{
    [SerializeField] ARFaceManager faceManager;
    [SerializeField] Material[] masks;
     private int selectedMaskIndex;

    private void OnEnable()
    {
        if (faceManager == null)
            return;

        faceManager.trackablesChanged.AddListener(OnFacesChanged);
        SelectMask(selectedMaskIndex);
    }

    private void OnDisable()
    {
        if (faceManager != null)
            faceManager.trackablesChanged.RemoveListener(OnFacesChanged);
    }

    public void SelectMask(int index)
    {
        if (faceManager == null || masks == null ||
            index < 0 || index >= masks.Length || masks[index] == null)
            return;

        selectedMaskIndex = index;

        foreach (ARFace face in faceManager.trackables)
            ApplyMask(face);
    }

    private void OnFacesChanged(ARTrackablesChangedEventArgs<ARFace> changes)
    {
        foreach (ARFace face in changes.added)
            ApplyMask(face);
    }

    private void ApplyMask(ARFace face)
    {
        if (masks == null || selectedMaskIndex >= masks.Length)
            return;

        Material mask = masks[selectedMaskIndex];
        if (mask == null)
            return;

        if (face.TryGetComponent<MeshRenderer>(out var meshRenderer))
            meshRenderer.sharedMaterial = mask;
    }
}

