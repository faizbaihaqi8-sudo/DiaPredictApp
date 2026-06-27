# DiaPredictApp 

Aplikasi screening risiko diabetes berbasis **.NET MAUI** yang mengintegrasikan **Fuzzy Logic (Sugeno)**, **AI Assistant**, dan **Explainable AI (XAI)** dalam satu aplikasi edukatif.

> Tugas Besar Mata Kuliah **Algoritma & Pemrograman**
> **Mochamad Baihaqi Faiz Sandia** — NRP 204225001
> Departemen Teknik Instrumentasi, Institut Teknologi Sepuluh Nopember (ITS)

---

#Tentang Aplikasi

DiaPredictApp adalah prototipe aplikasi untuk membantu **screening awal risiko diabetes** (bukan alat diagnosis medis). Aplikasi menghitung tingkat risiko berdasarkan input sederhana lalu memberikan penjelasan yang mudah dipahami pengguna.

#Fitur Utama

- **Dua Peran Pengguna:** Pasien & Tenaga Kesehatan (Nakes)
- **Prediksi Risiko (Fuzzy Sugeno):** Menghitung skor risiko dari gula darah puasa, BMI, dan riwayat keluarga
- **Verifikasi Wajah:** Login dengan pengenalan wajah (OpenCvSharp)
- **DiaAssist (AI Chatbot):** Asisten berbasis Gemini API yang bersifat **non-diagnostik**
- **Explainable AI (XAI):** Penjelasan hasil prediksi dengan bahasa yang ramah pengguna
- **Penyimpanan Lokal:** Database SQLite (`diapredict.db3`)

#Teknologi yang Digunakan

Framework = .NET MAUI 
Bahasa = C# & XAML 
Database = SQLite 
AI Assistant = Google Gemini API 
Computer Vision = OpenCvSharp 

#Logika Fuzzy (Sugeno)

- **Input:** Gula darah puasa, BMI, Riwayat keluarga
- **6 aturan (rules)** fuzzy
- **Membership Function:** left-shoulder, triangle, right-shoulder
- **Operator:** AND = min, OR = max
- **Defuzzifikasi:** weighted average
- **Kategori hasil:** Rendah (<40) · Sedang (40–64) · Tinggi (≥65)

#Cara Menjalankan

1. Clone repository ini:
git clone https://github.com/faizbaihaqi8-sudo/DiaPredictApp.git
2. Buka file `DiaPredictApp.sln` dengan **Visual Studio 2022** (workload **.NET MAUI** terinstal).
3. Masukkan **API Key Gemini** kamu di file `Services/GeminiApiService.cs`.
4. Pilih target device (Windows / Android), lalu tekan **Run** (F5).

#Struktur Project
DiaPredictApp/
-Models/        # Kelas data (UserAccount, DiabetesPredictionResult, dll)
-Pages/         # Halaman UI (.xaml)
-Services/      # Logika layanan (SQLite, Gemini, Face Recognition)
-Platforms/     # Konfigurasi platform (Android, iOS, Windows)
-Resources/     # Aset (gambar, font, ikon)
-DiaPredictApp.sln
