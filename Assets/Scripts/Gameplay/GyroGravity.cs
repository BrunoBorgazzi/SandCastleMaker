using UnityEngine;

public class GyroGravity : MonoBehaviour
{
    [Header("Configuración de Gravedad")]
    [Tooltip("Fuerza de la gravedad. 9.81 es el estándar de la Tierra.")]
    public float gravityMultiplier = 9.81f; 

    private void Start()
    {
        // En móviles, es buena idea evitar que la pantalla se apague sola mientras juegan
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }

    private void Update()
    {
        Vector2 newGravity = Physics2D.gravity;

// Estas directivas le dicen a Unity: "Usa este código solo si estoy en PC o en el Editor"
#if UNITY_EDITOR || UNITY_STANDALONE
        
        // --- CONTROLES PARA PC (Editor) ---
        // Como tu PC no tiene giroscopio, usamos A/D o Flechas Izq/Der para simular que inclinas el celular
        float tiltX = Input.GetAxis("Horizontal");
        
        // Hacemos que la gravedad apunte hacia abajo (-1) y le sumamos la inclinación lateral
        newGravity = new Vector2(tiltX, -1f).normalized * gravityMultiplier;
        
#else
        // --- CONTROLES PARA MÓVIL (Acelerómetro) ---
        // Usamos el acelerómetro porque en 2D es mucho más estable y directo que Input.gyro
        // Input.acceleration ya nos da un vector de gravedad (X para los lados, Y para arriba/abajo)
        newGravity = new Vector2(Input.acceleration.x, Input.acceleration.y) * gravityMultiplier;
#endif

        // Aplicamos la gravedad calculada a todo el motor de físicas de Unity
        Physics2D.gravity = newGravity;
    }
}
