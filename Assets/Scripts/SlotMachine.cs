using UnityEngine;

public class SlotMachine : MonoBehaviour
{
    [SerializeField] private Color[] _options;
    [SerializeField] private Cycler[] _slots;

    private void OnEnable()
    {
        foreach (var slot in _slots)
        {
            slot.Revolved += SwitchMaterial;
        }        
    }

    private void OnDisable()
    {
        foreach (var slot in _slots)
        {
            slot.Revolved -= SwitchMaterial;
        }
    }

    public void Spin()
    {   
        foreach (var slot in _slots)
        {
            slot.Spin();
        }
    }

    private void SwitchMaterial(Cycler cycler)
    {
        Debug.Log($"cycler sent switch: {cycler.gameObject.name}, {cycler.SlotIndex}");
        int newMat = Random.Range(0, _options.Length);
        cycler.ChangeSlot(_options[newMat]);
    }

}
