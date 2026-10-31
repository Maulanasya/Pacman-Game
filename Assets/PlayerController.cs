using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

public class PlayerController : MonoBehaviour
{
    [Header("Pengaturan Pergerakan")]
    public float kecepatan = 5f;

    [Header("UI & Koin")]
    public TextMeshProUGUI teksSkor;
    public int totalKoin = 10;
    
    private int skor = 0;
    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        
        // Menghitung total koin aktif di scene secara otomatis saat game dimulai
        totalKoin = GameObject.FindGameObjectsWithTag("Koin").Length;
    }

    void Update()
    {
        Vector2 gerak = Vector2.zero;
        var kb = Keyboard.current;

        // Membaca input pergerakan (WASD & Tombol Panah)
        if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) gerak.x = -1;
        if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) gerak.x = 1;
        if (kb.downArrowKey.isPressed || kb.sKey.isPressed) gerak.y = -1;
        if (kb.upArrowKey.isPressed || kb.wKey.isPressed) gerak.y = 1;

        // Menerapkan kecepatan ke komponen Rigidbody2D
        rb.linearVelocity = gerak * kecepatan;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Pengambilan koin & pembaruan UI skor
        if (other.CompareTag("Koin"))
        {
            skor++;
            
            // Cek kondisi menang jika semua koin sudah terkumpul
            if (skor >= totalKoin)
                teksSkor.text = "MENANG! Skor: " + skor;
            else
                teksSkor.text = "Skor: " + skor;

            Destroy(other.gameObject);
        }

        // Restart level jika bertabrakan dengan musuh
        if (other.CompareTag("Musuh"))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}