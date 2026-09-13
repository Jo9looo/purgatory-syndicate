# Purgatory Syndicate — Development Plan

> Pegangan utama development. Update dokumen ini setiap kali ada progress atau perubahan desain.
> Terakhir diupdate: 1 September 2026

---

## 1. Visi Game

**Genre:** 2.5D Turn-Based Tactical RPG
**Tema:** Tactical Exorcism — sindikat pengusir iblis di purgatory
**Inspirasi mekanik:** Limbus Company & Library of Ruina

**Pilar desain:**
1. **Clash System** — dua skill beradu head-on, power ditentukan coin roll
2. **Speed-Based Ordering** — roll speed tiap turn menentukan urutan aksi
3. **Sin Affinity** — 7 dosa (Wrath, Sloth, Gluttony, Gloom, Pride, Envy, Lust) sebagai resource & typing system
4. **Stagger** — bar kedua di bawah HP; habis = unit rentan

**Target fase sekarang:** WHITEBOX — buktikan core battle loop fun & benar memakai capsule primitif, tanpa art/animasi/UI final.

**Definisi selesai whitebox:** Bisa memainkan 1 encounter (minimal 2v1) dari awal sampai menang/kalah, dengan pemain memilih skill & target sendiri, dan semua mekanik inti (clash, coin, stagger, sin bonus) berfungsi serta terbaca dari HUD debug.

---

## 2. Status Saat Ini

### Sudah ada ✅
| Area | Detail |
|------|--------|
| Data layer | `SkillData`, `CharacterBaseStats` (ScriptableObject) |
| Enums | `SinType` (7 sin), `BattleState` (5 fase turn) |
| Entity | `BattleEntity` — HP/Stagger/Speed runtime |
| Manager | `BattleManager` — state machine, action queue, sort by speed |
| Clash | Deteksi head-on + formula prototype `base + (roll 0/1 × coin)` |
| Test harness | `BattleTestBootstrap` (auto-setup), `BattleTestInput` (SPACE/R), `BattleTestHUD` (OnGUI), `BattleEntityPulse` (flash) |
| Assets | 2 stats + 2 skill di `Resources/Battle/` |

### Keterbatasan sekarang ⚠️
- HUD masih OnGUI (bukan UGUI); belum ada animasi/VFX/SFX final
- Balancing angka (power, HP, stagger, sin cost, potency) belum di-playtest serius
- Status effect masih 4 jenis (Bleed / Fragile / PowerUp / PowerDown) — bisa ditambah nanti
- Encounter berantai memakai karakter yang sama dengan jumlah musuh naik (belum roster baru)

---

## 3. Roadmap Whitebox

### Sprint 1 — "Clash Matters" 🎯 (PRIORITAS SEKARANG)
Tujuan: clash punya konsekuensi nyata, battle bisa berakhir.

- [x] **1.1 Per-coin roll** — tiap coin di-flip satu per satu: `power += 1` per head; log tiap flip
- [x] **1.2 Clash damage** — loser menerima damage = full power winner (keputusan playtest awal)
- [x] **1.3 Stagger damage** — clash loser juga kehilangan stagger (1:1 dengan damage)
- [x] **1.4 One-sided attack** — aksi tanpa counter langsung apply full damage ke target
- [x] **1.5 Win/Lose condition** — semua enemy mati = WIN, semua ally mati = LOSE; tampil di HUD
- [x] **1.6 Fix scene wiring** — placeholder `Ally_1`/`Enemy_1` dinonaktifkan; entity di-spawn dari `Encounter_Default` (tidak ada duplikat)

**Test keberhasilan:** Tekan SPACE berulang → HP turun tiap clash → salah satu pihak mati → HUD tampilkan hasil battle.

### Sprint 2 — "Player Can Play"
Tujuan: pemain mengendalikan battle, bukan demo otomatis.

- [x] **2.1 EntityTeam enum** (Ally/Enemy) di `BattleEntity` — filter target valid
- [x] **2.2 CharacterDefinition SO** — stats + daftar 3 skill + sin affinities dalam satu asset
- [x] **2.3 Fase Targeting interaktif** — keyboard: `1/2/3` pilih skill, `Tab` ganti target, `Enter` confirm
- [x] **2.4 Enemy AI sederhana** — pilih skill & target random (rule-based menyusul)
- [x] **2.5 Turn menunggu semua action** — execution baru jalan setelah semua unit submit + Enter
- [x] **2.6 Hapus auto-demo** — `RunDemoTurn` dihapus; flow murni player-driven

**Test keberhasilan:** Main 1v1 penuh dengan memilih skill & target sendiri sampai menang/kalah.

### Sprint 3 — "Theme Mechanics"
Tujuan: mekanik khas game mulai hidup.

- [x] **3.1 Sin Affinity bonus** — skill match affinity karakter = +2 power flat (`SinAffinityBonus`)
- [x] **3.2 Stagger break** — stagger = 0 → tidak bisa aksi + damage ×2; pulih penuh di TurnEnd
- [x] **3.3 Multi-unit** — encounter default 2v2; speed ordering & targeting per unit
- [x] **3.4 EncounterData SO** — `Encounter_Default` (Exorcist+Priestess vs Demon+Imp)
- [x] **3.5 Unit mati dihapus dari queue** — aksi dibatalkan; target mati → serangan dialihkan (redirect)

**Test keberhasilan:** Encounter 2v2 dari `EncounterData`, stagger break terjadi, sin bonus terlihat di log.

### Sprint 4 — "Readable Whitebox"
Tujuan: whitebox mudah dibaca & didemokan.

- [x] **4.1 HP/Stagger bar** di atas tiap capsule (OnGUI world-space — cukup untuk whitebox; UGUI nanti)
- [x] **4.2 Action timeline panel** — urutan aksi tampil di fase ActionSorting sebelum Enter
- [x] **4.3 Battle log panel** — panel kanan, history lengkap (koin, damage, stagger, dll.)
- [x] **4.4 Restart battle** — tombol `R` kapan saja, tanpa re-Play
- [x] **4.5 Cleanup** — `BattleTest*` dihapus, diganti `BattleBootstrap`/`BattleHUD`/`BattleInputController`

**Test keberhasilan:** Orang lain bisa main & paham battle tanpa buka Console.

### Post-Whitebox (JANGAN dikerjakan sekarang)
Animasi, VFX coin/clash, SFX/music, camera work, art karakter, story/dialogue, meta progression, save/load, balancing serius.

---

## 4. Spesifikasi Mekanik (Target Design)

### 4.1 Turn Flow
```
RollSpeed      → semua unit roll speed dari speedRange
Targeting      → pemain & AI pilih skill + target per unit
ActionSorting  → queue disort by speed (tinggi dulu)
Execution      → resolve aksi berurutan; head-on = clash
TurnEnd        → cleanup, cek win/lose, lanjut turn berikutnya
```

### 4.2 Clash (target Sprint 1)
- Head-on: A menyerang B **dan** B menyerang A → clash
- Tiap sisi roll power: `basePower + jumlah head dari coinCount flip`
- Power lebih tinggi menang; loser terima **damage HP = power winner** (atau selisih — tentukan saat playtest) + **stagger damage**
- Draw: re-roll (atau keduanya batal — tentukan saat playtest)
- One-sided (target tidak melawan): full damage langsung

### 4.3 Coin
- 1 coin = 1 flip, head = +1 power (nilai per-coin bisa jadi properti skill nanti)
- Kalah clash SEHARUSNYA menghilangkan 1 coin lalu re-clash (rule Limbus) → **prototipe nanti di Sprint 3+**, untuk sekarang single exchange dulu

### 4.4 Stagger
- Damage HP juga mengurangi stagger (rasio 1:1 dulu)
- Stagger 0 → **Staggered**: skip turn, damage diterima ×2, stagger reset penuh di turn berikutnya

### 4.5 Sin Affinity (rule awal, akan berevolusi)
- Karakter punya 1–2 sin affinity
- Skill dengan sinType yang match affinity → +2 power flat
- Ke depan: sin resource/cost, resonance chain, weakness table antar sin

---

## 5. Arsitektur & Konvensi

### Struktur folder
```
Assets/
├── Scripts/Battle/
│   ├── Enums/    → SinType, BattleState, (EntityTeam)
│   ├── Data/     → ScriptableObject definitions
│   └── Core/     → MonoBehaviour & logic runtime
├── Resources/Battle/  → asset SO yang di-load runtime
├── Scenes/
└── Materials/
```

### Aturan koding
- Namespace: `PurgatorySyndicate.Battle`
- Data = ScriptableObject (immutable), runtime state = MonoBehaviour/struct
- Logic battle TIDAK boleh bergantung pada UI; UI membaca state via property publik
- `Debug.Log` dengan prefix `[NamaClass]` untuk semua event penting
- Input pakai **Input System baru** (`Keyboard.current`), bukan `UnityEngine.Input`
- Script test/debug diberi prefix `BattleTest*` supaya mudah dibersihkan nanti

### File utama & tanggung jawab
| File | Tanggung jawab |
|------|----------------|
| `BattleManager` | State machine, queue, resolusi clash — SATU-satunya yang mengubah flow |
| `BattleEntity` | State per unit (HP/Stagger/Speed), damage intake |
| `SkillData` / `CharacterBaseStats` | Data murni, tidak ada logic |
| `BattleTest*` | Harness testing — boleh dibuang setelah whitebox |

---

## 6. Kontrol Testing (Sekarang)

| Input | Fase | Fungsi |
|-------|------|--------|
| 1 / 2 / 3 | Targeting | Pilih skill unit yang sedang giliran |
| Tab | Targeting | Ganti target musuh |
| Enter | Targeting | Konfirmasi aksi unit → lanjut unit berikutnya |
| Enter / Space | ActionSorting | Eksekusi turn (setelah timeline tampil) |
| R | Kapan saja | Restart battle dari awal |

HUD: panel kiri atas = instruksi per fase + skill picker; di atas capsule = nama, bar HP (hijau) & Stagger (kuning), marker GILIRAN/TARGET; panel kanan = battle log lengkap.

---

## 7. Keputusan Desain (diputuskan 1 Sep 2026 — bisa direvisi via playtest)

- [x] Damage clash = **full power winner** (bukan selisih) — lebih terasa berisiko
- [x] Draw clash = **keduanya batal** tanpa damage
- [x] Coin loss saat kalah clash (rule Limbus penuh) = **post-whitebox**
- [x] Stagger damage = **1:1 dengan raw damage**; saat staggered, HP damage ×2 dan stagger tidak turun
- [x] Speed tie-break: **Ally dulu**, lalu alfabetis
- [x] Target mati sebelum aksi = **redirect ke lawan hidup acak** (bukan batal)
- [x] Sin bonus = **+2 flat** jika sinType skill ada di sinAffinities karakter

---

## 8. Log Progress

| Tanggal | Update |
|---------|--------|
| 9 Agu 2026 | Fondasi: enums, SO data, BattleEntity, BattleManager, clash prototype, test harness (bootstrap/HUD/input/pulse) |
| 1 Sep 2026 | Audit project penuh; dokumen plan ini dibuat |
| 1 Sep 2026 | Sprint 1–4 whitebox selesai: per-coin roll, clash/one-sided damage, stagger break, win/lose, targeting interaktif (1/2/3+Tab+Enter), enemy AI, sin bonus +2, EncounterData 2v2, HUD lengkap (bar, timeline, log), restart (R). Script test lama dihapus, diganti BattleBootstrap/BattleHUD/BattleInputController/TargetingController. Karakter baru: Priestess & Imp; 5 skill baru. |
| 1 Sep 2026 | Feedback visual & HUD: animasi whitebox (`BattleEntityMotion` — lunge/clash di tengah/hit shake/knockback), damage popup melayang (-X, power roll, STAGGER BREAK!, GUGUR, PULIH), banner tengah layar (TURN X/CLASH!/hasil), bar HP & Stagger dengan angka + label sin affinity, panel kontrol kiri. Fix: stagger break kini benar-benar skip 1 turn penuh (`PendingStaggerRecovery`). |
| 13 Sep 2026 | Main menu UGUI: scene `MainMenu` (entry point, index 0 di Build Settings) + `MainMenuController` (Canvas dibangun runtime — judul, MULAI, KELUAR; EventSystem pakai InputSystemUIInputModule). Tombol `M` di BattleOver = kembali ke menu. Fix: `BattleBootstrap` kini hook `sceneLoaded` sehingga battle tetap ter-setup saat masuk dari menu (bukan hanya direct Play). Visual menu masih placeholder — reskin dengan aset Aseprite nanti. |
| 13 Sep 2026 | Overhaul kontrol targeting jadi drag & drop mouse: `BattleMouseController` (raycast hover + drag), `TargetArrow` (panah lengkung LineRenderer — kuning saat drag, merah saat terkunci, persist sampai TurnEnd, bisa di-drag ulang pindah target), `TargetingController` ditulis ulang jadi model assignment (bukan urutan unit). Keyboard: 1/2/3 ganti skill unit yang di-hover/drag, ENTER kunci semua aksi. Tooltip statistik lengkap saat hover unit (base stat, kondisi, skill, buff/debuff placeholder). HUD: marker "DRAG AKU"/"DROP DI SINI", label skill di puncak panah, daftar assignment di panel kiri atas. `BattleEntity.StatusEffects` placeholder untuk sistem buff/debuff nanti. |
| 13 Sep 2026 | Skill bar bawah-tengah + skill defensif: setiap karakter kini punya 3 skill aktif + 1 skill bertahan (`SkillKind`: Block/Dodge/Counter, field `defenseSkill` di CharacterDefinition). Bar menampilkan 4 slot klik-able untuk ally yang di-hover (sticky), tombol 1-4. Slot 4 = bertahan (self-target, tanpa drag panah). Mekanik: stance aktif di AWAL eksekusi (sebelum serangan mana pun) dan bertahan 1 turn — BLOCK kurangi damage masuk (default -50%), DODGE peluang hindar total (default 50%), COUNTER balas serang setelah kena pukul (Pow+koin, tidak berantai). AI musuh 25% memilih bertahan. Aset baru: `Skill_IronBlock` (Priestess & Demon), `Skill_ShadowDodge` (Imp), `Skill_VengefulCounter` (Exorcist). |
| 13 Sep 2026 | Polish kontrol + clash multi-ronde: (1) `SelectionMarker` — segitiga emas melayang (bob + billboard) di atas ally yang sedang dikontrol; logika seleksi dipindah ke `BattleMouseController.SelectedAlly`. (2) Slot skill di bar bisa di-drag ke musuh (`BeginSkillDrag`) — panah tetap menyambung dari karakter, klik biasa = pilih skill saja. (3) Clash dirombak jadi multi-ronde ala Limbus: tiap ronde kedua sisi roll power dengan SISA koin masing-masing, kalah ronde = kehilangan 1 koin, seri = reroll (cap 12 ronde), pemenang menyerang penuh dengan sisa koinnya (`RollPower` dengan coin count override). |
| 13 Sep 2026 | Whitebox depth pass: buff/debuff sungguhan (`Bleed` saat beraksi, `Fragile` +% damage masuk, `PowerUp`/`PowerDown` di roll), tabel kelemahan sin (Wrath>Sloth>Gluttony>Gloom>Pride>Envy>Lust>Wrath — EFEKTIF +50% / TAHAN -25%) + resonance (+1 power per extra same-sin attacker satu tim), AI musuh rule-based (defend stagger kritis, incar HP terendah, pilih skill efektif/affordable), run 3 stage (`Encounter_Stage1/2/3`, N lanjut setelah menang, M/menu reset), resource Sin (+1/turn, skill punya `sinCost`) + cooldown, timeline visual horizontal di atas tengah. Skill assets diisi efek/cost. HUD tooltip & skill bar baca `ActiveEffects`, CD, dan biaya Sin. |
