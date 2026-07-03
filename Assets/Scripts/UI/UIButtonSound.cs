using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIButtonSound : MonoBehaviour
{
    private void Start()
    {
        Button btn = GetComponent<Button>();
        
        // Agregamos un listener automático para que cuando se haga click, 
        // llame a nuestro método con pitch aleatorio en el AudioManager
        btn.onClick.AddListener(() => 
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayButtonSound();
            }
        });
    }
}
