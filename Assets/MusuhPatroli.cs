using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class MusuhPatroli : MonoBehaviour
{
    [Header("Kecepatan Musuh")]
    public float kecepatan = 3.5f;

    [Header("Area Labirin")]
    public bool otomatisBatas = true;
    public Vector2 batasMin = new Vector2(-10f, -6f);
    public Vector2 batasMax = new Vector2(10f, 6f);
    public float ukuranKotak = 0.25f;

    [Header("Ukuran Badan")]
    public bool otomatisUkuranBadan = true;
    public float ukuranBadan = 0.85f;
    public float marginBadan = 0.04f;

    [Header("Pengejaran")]
    public float intervalRute = 0.15f;
    public float batasMacet = 0.6f;

    [Header("Patroli Jika Player Tidak Ada")]
    public float waktuPatroli = 1f;

    private Rigidbody2D rb;
    private Transform player;

    private bool[,] terhalang;
    private int lebar;
    private int tinggi;

    private Vector2 titikTujuan;

    private float timerRute;
    private float timerPatroli;
    private float timerMacet;

    private bool punyaTujuan;

    // Arah pergerakan tetangga (Atas, Bawah, Kiri, Kanan)
    private Vector2Int[] tetangga =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        // Mengunci rotasi dan gravitasi agar pergerakan grid stabil
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        var pc = FindFirstObjectByType<PlayerController>();

        if (pc != null)
        {
            player = pc.transform;
            Debug.Log("PLAYER BERHASIL DITEMUKAN: " + player.name);
        }
        else
        {
            Debug.LogError("PLAYER TIDAK DITEMUKAN! Periksa PlayerController.");
        }

        Physics2D.SyncTransforms();

        if (otomatisUkuranBadan) HitungUkuranBadan();
        if (otomatisBatas) HitungBatasOtomatis();

        BuatGrid();
        Debug.Log("GRID BERHASIL DIBUAT: " + lebar + " x " + tinggi);

        // Menyesuaikan posisi awal musuh ke tengah sel grid terdekat
        rb.position = PusatKotak(KeKotak(rb.position));

        CariRute();
    }

    // Memfilter objek collider agar hanya dinding/rintangan yang diproses
    bool LayakRintangan(Collider2D c)
    {
        if (c.isTrigger) return false;
        if (c.GetComponentInParent<MusuhPatroli>() != null) return false;
        if (player != null && c.transform.root == player.root) return false;
        return true;
    }

    // Menghitung ukuran bounding box musuh secara otomatis dari Collider2D
    void HitungUkuranBadan()
    {
        float maks = 0f;

        foreach (Collider2D c in GetComponents<Collider2D>())
        {
            if (c.isTrigger) continue;
            Vector3 s = c.bounds.size;
            maks = Mathf.Max(maks, s.x, s.y);
        }

        if (maks > 0f) ukuranBadan = maks + marginBadan;
    }

    // Kalkulasi otomatis batas minimum dan maksimum arena berdasarkan collider dinding
    void HitungBatasOtomatis()
    {
        Collider2D[] semua = Physics2D.OverlapAreaAll(
            new Vector2(-500f, -500f),
            new Vector2(500f, 500f)
        );

        bool ada = false;
        Bounds total = new Bounds();

        foreach (Collider2D c in semua)
        {
            if (!LayakRintangan(c)) continue;

            if (!ada)
            {
                total = c.bounds;
                ada = true;
            }
            else
            {
                total.Encapsulate(c.bounds);
            }
        }

        if (ada)
        {
            batasMin = total.min;
            batasMax = total.max;
        }
        else
        {
            Debug.LogWarning("Dinding tidak ditemukan, memakai batasMin/batasMax manual.");
        }
    }

    // Memetakan arena ke grid dan mendeteksi sel yang terhalang rintangan
    void BuatGrid()
    {
        lebar = Mathf.CeilToInt((batasMax.x - batasMin.x) / ukuranKotak);
        tinggi = Mathf.CeilToInt((batasMax.y - batasMin.y) / ukuranKotak);

        terhalang = new bool[lebar, tinggi];

        for (int x = 0; x < lebar; x++)
        {
            for (int y = 0; y < tinggi; y++)
            {
                Vector2 pusat = PusatKotak(new Vector2Int(x, y));

                Collider2D[] hasil = Physics2D.OverlapBoxAll(
                    pusat,
                    new Vector2(ukuranBadan, ukuranBadan),
                    0f
                );

                foreach (Collider2D c in hasil)
                {
                    if (!LayakRintangan(c)) continue;

                    terhalang[x, y] = true;
                    break;
                }
            }
        }
    }

    // Konversi indeks Grid ke posisi World Space (Vector2)
    Vector2 PusatKotak(Vector2Int k)
    {
        return new Vector2(
            batasMin.x + (k.x + 0.5f) * ukuranKotak,
            batasMin.y + (k.y + 0.5f) * ukuranKotak
        );
    }

    // Konversi posisi World Space ke indeks Grid
    Vector2Int KeKotak(Vector2 posisi)
    {
        int x = Mathf.Clamp(Mathf.FloorToInt((posisi.x - batasMin.x) / ukuranKotak), 0, lebar - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt((posisi.y - batasMin.y) / ukuranKotak), 0, tinggi - 1);

        return new Vector2Int(x, y);
    }

    void FixedUpdate()
    {
        if (rb == null || terhalang == null) return;

        // Pencarian rute berkala jika musuh belum memiliki target titik
        if (!punyaTujuan)
        {
            timerRute -= Time.fixedDeltaTime;

            if (timerRute <= 0f)
            {
                timerRute = intervalRute;
                CariRute();
            }
        }

        // Mode patroli acak jika player tidak terjangkau/ditemukan
        if (!punyaTujuan)
        {
            timerPatroli -= Time.fixedDeltaTime;

            if (timerPatroli <= 0f)
            {
                CariLangkahAcak();
                timerPatroli = waktuPatroli;
            }
        }

        // Pergerakan musuh ke titik tujuan dan penanganan kondisi macet (stuck)
        if (punyaTujuan)
        {
            rb.linearVelocity = Vector2.zero;

            Vector2 baru = Vector2.MoveTowards(
                rb.position,
                titikTujuan,
                kecepatan * Time.fixedDeltaTime
            );

            rb.MovePosition(baru);
            timerMacet += Time.fixedDeltaTime;

            // Jika sudah sampai sel tujuan
            if ((baru - titikTujuan).sqrMagnitude <= 0.000001f)
            {
                punyaTujuan = false;
                timerMacet = 0f;
                timerRute = 0f;
            }
            // Reset rute jika karakter tersangkut melebihi batasMacet
            else if (timerMacet > batasMacet)
            {
                punyaTujuan = false;
                timerMacet = 0f;
                timerRute = 0f;
            }
        }
        else
        {
            timerMacet = 0f;
        }
    }

    // Menentukan rute pengejaran ke posisi player
    void CariRute()
    {
        if (player == null)
        {
            CariLangkahAcak();
            return;
        }

        Vector2Int awal = KeKotak(rb.position);
        Vector2Int tujuan = CariSelBebasTerdekat(KeKotak(player.position));

        if (awal == tujuan)
        {
            titikTujuan = player.position;
            punyaTujuan = true;
            timerMacet = 0f;
            return;
        }

        Vector2Int? langkah = CariLangkahBerikut(awal, tujuan);

        if (langkah.HasValue)
        {
            titikTujuan = PusatKotak(langkah.Value);
            punyaTujuan = true;
            timerMacet = 0f;
        }
        else
        {
            CariLangkahAcak();
        }
    }

    // Mencari sel terdekat yang aman jika posisi player berada di sel yang terhalang
    Vector2Int CariSelBebasTerdekat(Vector2Int t)
    {
        if (!terhalang[t.x, t.y]) return t;

        for (int r = 1; r <= 8; r++)
        {
            bool ketemu = false;
            Vector2Int terbaik = t;
            float jarakTerbaik = float.MaxValue;

            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;

                    Vector2Int n = new Vector2Int(t.x + dx, t.y + dy);

                    if (!DalamGrid(n) || terhalang[n.x, n.y]) continue;

                    float j = dx * dx + dy * dy;
                    if (j < jarakTerbaik)
                    {
                        jarakTerbaik = j;
                        terbaik = n;
                        ketemu = true;
                    }
                }
            }

            if (ketemu) return terbaik;
        }

        return t;
    }

    // Algoritma BFS untuk mencari 1 langkah awal terpendek menuju target
    Vector2Int? CariLangkahBerikut(Vector2Int awal, Vector2Int tujuan)
    {
        if (!DalamGrid(tujuan) || terhalang[tujuan.x, tujuan.y])
            return null;

        Queue<Vector2Int> antrian = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> asal = new Dictionary<Vector2Int, Vector2Int>();

        antrian.Enqueue(awal);
        asal[awal] = awal;

        while (antrian.Count > 0)
        {
            Vector2Int sekarang = antrian.Dequeue();

            foreach (Vector2Int d in tetangga)
            {
                Vector2Int n = sekarang + d;

                if (!DalamGrid(n) || asal.ContainsKey(n) || terhalang[n.x, n.y])
                    continue;

                asal[n] = sekarang;

                if (n == tujuan)
                {
                    Vector2Int langkah = n;
                    while (asal[langkah] != awal)
                    {
                        langkah = asal[langkah];
                    }
                    return langkah;
                }

                antrian.Enqueue(n);
            }
        }

        return null;
    }

    // Memastikan koordinat berada di dalam batas matriks grid
    bool DalamGrid(Vector2Int k)
    {
        return k.x >= 0 && k.y >= 0 && k.x < lebar && k.y < tinggi;
    }

    // Memilih sel tetangga secara acak untuk patroli
    void CariLangkahAcak()
    {
        Vector2Int posisi = KeKotak(rb.position);
        List<Vector2Int> pilihan = new List<Vector2Int>();

        foreach (Vector2Int d in tetangga)
        {
            Vector2Int n = posisi + d;

            if (!DalamGrid(n) || terhalang[n.x, n.y])
                continue;

            pilihan.Add(n);
        }

        if (pilihan.Count == 0)
        {
            punyaTujuan = false;
            return;
        }

        titikTujuan = PusatKotak(pilihan[Random.Range(0, pilihan.Count)]);
        punyaTujuan = true;
        timerMacet = 0f;
    }

    // Visualisasi grid, sel terhalang, dan garis pergerakan pada Unity Editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector2 pusat = (batasMin + batasMax) / 2f;
        Gizmos.DrawWireCube(pusat, batasMax - batasMin);

        if (Application.isPlaying && terhalang != null)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.35f);
            for (int x = 0; x < lebar; x++)
            {
                for (int y = 0; y < tinggi; y++)
                {
                    if (terhalang[x, y])
                        Gizmos.DrawCube(PusatKotak(new Vector2Int(x, y)),
                                        new Vector3(ukuranKotak, ukuranKotak, 0.01f));
                }
            }
        }

        if (Application.isPlaying && punyaTujuan)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, titikTujuan);
            Gizmos.DrawSphere(titikTujuan, 0.1f);
        }
    }
}