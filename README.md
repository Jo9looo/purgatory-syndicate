# Purgatory Syndicate

2.5D turn-based tactical RPG — **Tactical Exorcism**. Sindikat pengusir iblis di purgatory.

Inspirasi mekanik: [Limbus Company](https://limbuscompany.wiki.gg/) & Library of Ruina.  
Fase sekarang: **whitebox**. Battle loop sudah bisa dimainkan end-to-end dengan capsule, HUD debug, dan encounter 3 stage. Art / animasi / UI final belum.

## Butuh apa

- [Unity 6](https://unity.com/download) **6000.5.7f1** (lihat `ProjectSettings/ProjectVersion.txt`)
- Git (kalau clone dari GitHub)

## Cara buka project

1. Clone repo:

   ```bash
   git clone https://github.com/Jo9looo/purgatory-syndicate.git
   ```

2. Buka **Unity Hub → Add →** pilih folder hasil clone.
3. Kalau Unity minta versi editor, pakai **6000.5.7f1** (atau biarkan Hub download versi itu).
4. Tunggu Unity generate ulang folder `Library/` (tidak ikut di Git — itu normal).
5. Buka scene `Assets/Scenes/MainMenu.unity`, lalu tekan **Play**.

Tombol **MULAI** memuat battle. Scene battle (`SampleScene`) juga bisa di-Play langsung; `BattleBootstrap` akan spawn encounter sesuai stage.

## Cara main

Setiap karakter punya **3 skill aktif** + **1 skill bertahan** (Block / Dodge / Counter) di slot 4.

### Targeting

1. Segitiga emas menandai unit yang sedang kamu kontrol.
2. **Drag** dari ally ke musuh — panah muncul dari kepala karakter, drop untuk mengunci target.
3. Atau **tekan-tahan slot skill 1–3** di bar bawah, lalu drop ke musuh (panah tetap nyambung dari karakter).
4. Slot **4** = bertahan (tidak perlu drag).
5. Hover unit mana pun untuk lihat stat, skill, Sin, buff/debuff.
6. Setelah semua ally punya perintah, **Enter** untuk kunci → lihat timeline → **Enter / Space** untuk eksekusi.

Panah merah tetap terlihat sampai turn selesai. Drag ulang dari ally yang sama untuk pindah target.

### Keyboard

| Input | Kapan | Fungsi |
|---|---|---|
| 1 / 2 / 3 | Targeting | Pilih skill aktif (unit yang di-hover / di-drag) |
| 4 | Targeting | Skill bertahan |
| Enter | Targeting | Kunci semua aksi |
| Enter / Space | Timeline | Eksekusi turn |
| R | Kapan saja | Restart stage ini |
| N | Menang | Lanjut stage berikutnya |
| M | Battle over | Kembali ke main menu |

## Mekanik (whitebox)

**Clash multi-ronde** — kalau A menyerang B dan B menyerang A, keduanya maju dan adu power tiap ronde. Kalah ronde = kehilangan 1 koin. Habis koin = lawan mendaratkan serangan penuh dengan sisa koinnya.

**Power roll** — `basePower + jumlah head dari koin + bonus sin match (+2) + resonance + PowerUp − PowerDown`.

**Speed** — tiap turn unit roll speed; yang lebih tinggi bertindak lebih dulu (terlihat di timeline atas).

**Stagger** — bar di bawah HP. Habis = skip 1 turn penuh + damage diterima ×2, pulih di akhir turn berikutnya.

**Sin** — 7 dosa: Wrath, Sloth, Gluttony, Gloom, Pride, Envy, Lust.

- Affinity match = +2 power.
- Weakness cycle: Wrath > Sloth > Gluttony > Gloom > Pride > Envy > Lust > Wrath.  
  Unggul = **EFEKTIF +50%** damage, kalah = **TAHAN −25%**.
- Resonance: 2+ unit satu tim menyerang dengan sin yang sama = +1 power per extra penyerang.
- Resource Sin: +1 tiap awal turn (max 6). Skill punya biaya; slot abu-abu kalau Sin kurang atau sedang cooldown.

**Bertahan (1 turn)**

- Block — kurangi damage masuk (default −50%).
- Dodge — peluang hindar total (default 50%).
- Counter — tetap kena pukul, lalu balas (tidak berantai).

**Status** — Bleed (damage saat beraksi), Fragile (+% damage masuk), PowerUp / PowerDown (± power roll).

**AI musuh** — bertahan jika stagger kritis, incar ally HP terendah, pilih skill yang EFEKTIF / match affinity / bisa dibayar.

## Run

| Stage | Encounter |
|---|---|
| 1 | Purgatory Gate — First Contact (2v2) |
| 2 | Inner Court — The Pack (2v3) |
| 3 | Throne Depths — Last Rite (2v4) |

Menang → **N**. Kalah / **R** = ulang stage yang sama. **M** atau MULAI dari menu mereset ke stage 1.

Party default: **Exorcist** (Counter) + **Priestess** (Block) vs **Demon** (Block) + **Imp** (Dodge).

## Isi repo

```
Assets/Scripts/Battle/     logika battle (data, enums, runtime)
Assets/Resources/Battle/   ScriptableObject: skill, karakter, encounter
Assets/Scenes/             MainMenu, SampleScene (battle)
DEVELOPMENT_PLAN.md        roadmap & decision log
```

`Library/`, `Temp/`, `Logs/`, dan file IDE diabaikan lewat `.gitignore`. Jangan commit folder itu.

## Status

Whitebox playable. Yang belum: art Aseprite, UI UGUI final, VFX/SFX, balancing serius, roster musuh baru.

Detail desain dan log progress ada di [`DEVELOPMENT_PLAN.md`](DEVELOPMENT_PLAN.md).
