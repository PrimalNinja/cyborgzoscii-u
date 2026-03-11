# Protokol UNSIGNAL: Analisis Intelijen Langkah demi Langkah

**Penulis:** Julian Cassin  
**Tanggal:** 2026-03-11

1. Penyadapan
   - File mentah diperoleh: tanpa header, tanpa byte ajaib, tanpa struktur
   - Ukuran file: bervariasi (karena prefiks/sufiks acak)
   - Metadata: waktu, sumber, tujuan — tidak mengarah ke mana pun, tidak dapat dikorelasikan dengan konten atau niat

2. Analisis Statistik (ent)
   - Entropi: 7.99+ bit/byte (maksimum)
   - Kompresi: 0% (entropi sempurna)
   - Chi square: lolos sebagai acak
   - Korelasi serial: mendekati nol
   - Hasil: tidak dapat dibedakan dari derau acak sejati

3. Kebingungan Sistem Koordinat
   - Alamat header: posisi absolut dalam 64KB pertama ROM
   - Alamat data: relatif terhadap offset ROM yang diturunkan dari pengalihan H1/H2 itu sendiri
   - Dua sistem koordinat berbeda dalam file yang sama
   - Penyerang tidak dapat menafsirkan alamat data tanpa terlebih dahulu:
       a) Mengenali H1/H2 sebagai khusus (terlihat seperti data biasa)
       b) Mendekode H1/H2 untuk mendapatkan nilai offset
       c) Menerapkan offset untuk menafsirkan ulang semua alamat berikutnya
   - Probabilitas menebak dengan benar tanpa ROM: nol
   - Bahkan dengan ROM, harus tahu alamat mana yang header vs. data
   - Penyelarasan header/data hanya terjadi 1/65536 dari waktu secara kebetulan

4. Analisis Lalu Lintas
   - Prefiks/sufiks acak menyembunyikan batas pesan yang sebenarnya
   - H3/H4 juga merupakan pengalihan itu sendiri
   - Offset awal ROM yang bervariasi mengubah interpretasi per sesi
   - Tidak ada pola tetap dalam ukuran paket atau waktu
   - Tidak dapat menentukan apakah file berisi data atau kosong

5. Rekayasa Balik
   - Encoder diperoleh: tabel pencarian satu baris (publik)
   - Algoritma: sepele, keamanan ada di ROM (kunci)
   - Mengetahui cara kerjanya tidak memberikan keuntungan

6. Upaya Teks-Terkenal (Known-Plaintext)
   - Teks biasa yang sama dikodekan dua kali → keluaran berbeda
   - Banyak opsi alamat per karakter (pemilihan acak)
   - Tidak ada pola berulang yang dapat dieksploitasi

7. Pemulihan Kunci
   - Brute force: ITS, mustahil menurut definisi
   - Saluran samping: pencarian sederhana, tidak ada matematika rumit yang bocor
   - ROM harus diperoleh melalui cara fisik/hukum

8. Masalah Verifikasi
   - ROM apa pun mendekode sesuatu
   - ROM salah → sampah (tetapi sampah yang terlihat nyata)
   - Tidak ada checksum, tidak ada MAC, tidak ada indikator keberhasilan
   - Tidak dapat memverifikasi dekode mana yang "benar"

9. Skala Kombinatorial (contoh Gone with the Wind)
   - Pengkodean novel tunggal: >10^5,500,000 kemungkinan representasi
   - 5 alamat tidak berulang: 1 triliun kombinasi
   - Pelacakan hingga memori habis: mustahil
   - Tidak ada tabrakan, selamanya

10. Penyangkalan Sempurna
    - Setiap dekode konsisten secara internal
    - Keluaran apa pun dapat diabaikan sebagai kebetulan acak
    - Dekode "benar" tidak terdefinisi tanpa konteks eksternal

11. Perilaku Kompresi
    - File yang dikodekan ZOSCII/UNSIGNAL tidak terkompresi (~0% rasio)
    - Keluaran sudah mendekati entropi maksimum
    - Untuk pengurangan ukuran: kompres input terlebih dahulu, lalu kodekan
    - Hasil yang dikodekan tetap tidak dapat dikompresi terlepas dari inputnya

12. Autentikasi & Deteksi Gangguan Bersifat Internal
    - MAC jika diperlukan: tempatkan DI DALAM muatan yang dikodekan
    - Checksum, tanda tangan, data verifikasi: semuanya masuk DI DALAM pesan
    - Aturan pengkodean yang sama berlaku — mereka menjadi tidak dapat dibedakan dari derau acak
    - Penyerang tidak dapat membedakan data autentikasi dari konten pesan
    - Tidak ada penanda validasi eksternal yang ada

13. Kesimpulan
    - Alat statistik mengembalikan: derau acak, tidak ada apa-apa di sini
    - Analisis lalu lintas dikalahkan dengan menyembunyikan batas
    - Pemulihan kunci memerlukan ROM, bukan matematika
    - Verifikasi tidak mungkin dilakukan bahkan dengan kandidat ROM
    - Autentikasi tersembunyi di dalam muatan, tidak dapat dibedakan dari pesan
    - Kompresi hanya mungkin dilakukan sebelum pengkodean, bukan setelahnya
    - Sistem mencapai penutupan epistemik: penyerang tidak dapat tahu apakah mereka telah menang