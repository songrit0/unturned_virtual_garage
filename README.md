# Virtual Garage — Unturned RocketMod Plugin

Store your vehicles in a personal garage and pull them back out later — with the **full vehicle
state preserved**: fuel, health, battery, tires, lock/owner, paint, skin, the **trunk contents**, and
any **barricades mounted on the vehicle (lockers, safes, signs… including the items inside them)**.

Includes an optional **stand‑and‑wait "store channel"** (per‑vehicle timer + sound) so storing a
vehicle isn't instant. Data is stored in **MySQL / MariaDB**.

| | |
|---|---|
| Unturned | 3.26.3.2 |
| RocketMod | 4.9.3.18 (LDM / FPlugins compatible) |
| Target framework | .NET Framework 4.8 |
| Storage | MySQL / MariaDB **or** local XML file |
| Dependency | `MySql.Data.dll` (only for MySQL mode — see Requirements) |

---

## คุณสมบัติ / Features

- 🚗 **เก็บ/เรียกรถ** — `/gadd` เก็บเข้าอู่, `/gretrieve` เรียกออกมา
- 💾 **เก็บสถานะเต็ม** — fuel, health, battery, tire mask, locked + owner/group, paint colour, skin id
- 📦 **เก็บของในกระโปรง (trunk)** ครบ (id, จำนวน, quality, state)
- 🔒 **เก็บของตกแต่งบนรถ** — barricade ที่ติดบนรถ (ตู้เซฟ/ล็อกเกอร์/ป้าย) + **ของข้างในตู้** (อยู่ใน barricade state)
- 🧹 **ไม่มีของซ้ำ** — เคลียร์ของในกระโปรง/ตู้ก่อนทำลายรถ ของจึงไม่หล่นบนพื้นตอนเก็บ
- ⏳ **Store channel (ยืนรอเก็บ)** — ตั้งเวลาต่อ vehicle id, ขยับ = ยกเลิก, มีเสียงให้คนรอบๆ ได้ยิน
- 🛠️ **คำสั่งแอดมิน** — เก็บ/เพิ่ม/เรียกรถให้ผู้เล่นคนอื่น (เก็บทันที ไม่ต้องรอ)
- 🌐 **ข้อความ 2 ภาษา (EN | TH)** ปรับสีได้

---

## ความต้องการ / Requirements

1. **Unturned Dedicated Server** + **RocketMod** (4.9.3.x)
2. **(เฉพาะโหมด MySQL)** MySQL / MariaDB database + user — **ไม่จำเป็นถ้าใช้โหมด FILE**
3. **(เฉพาะโหมด MySQL)** `MySql.Data.dll` in the server's `Rocket/Libraries/` folder
   - ไม่ได้แถมมาใน repo นี้ (เรื่องลิขสิทธิ์ของ Oracle)
   - เซิร์ฟเวอร์ส่วนใหญ่ที่มีปลั๊กอินตระกูล MySQL (MySQLKits, MySQLVault, PlayerStats ฯลฯ) **มีไฟล์นี้อยู่แล้ว** ใน `Rocket/Libraries/`
   - ถ้าไม่มี: ดาวน์โหลด `MySql.Data` 6.9.12 จาก [nuget.org](https://www.nuget.org/packages/MySql.Data/6.9.12) แล้วเอา `lib/net45/MySql.Data.dll` ไปวางใน `Rocket/Libraries/`

> The plugin compiles against MySql.Data **6.9.12** but only uses the standard ADO.NET API
> (`MySqlConnection`, `MySqlCommand`, `MySqlDataReader`), so it runs fine against newer versions
> (e.g. 8.0.30) already present on the server — RocketMod resolves the dependency by name.

---

## การติดตั้ง / Installation

```
Servers/<server>/Rocket/Plugins/VirtualGarage/VirtualGarage.dll
Servers/<server>/Rocket/Libraries/MySql.Data.dll      (ต้องมีอยู่แล้ว / provide it)
```

1. วาง `VirtualGarage.dll` (จากโฟลเดอร์ `dist/`) ไว้ใน `Rocket/Plugins/VirtualGarage/`
2. ตรวจว่ามี `MySql.Data.dll` ใน `Rocket/Libraries/`
3. สร้าง database + grant สิทธิ์ให้ user:
   ```sql
   CREATE DATABASE IF NOT EXISTS s203_unturned CHARACTER SET utf8mb4;
   GRANT ALL PRIVILEGES ON s203_unturned.* TO 'u203_xxxx'@'%';
   FLUSH PRIVILEGES;
   ```
4. เปิดเซิร์ฟ 1 ครั้ง → ปลั๊กอินสร้างไฟล์ `VirtualGarage.configuration.xml` และตาราง `virtual_garage` ให้เอง
5. แก้ค่า MySQL ในไฟล์ config → **รีสตาร์ตเซิร์ฟ**
6. ดู console ต้องขึ้น: `VirtualGarage loaded. Database OK (table 'virtual_garage').`

> **Pterodactyl:** ใช้ค่า Host/Port จากแท็บ **Databases** ของ panel (อย่าใช้ `host.docker.internal` ถ้า phpMyAdmin มองไม่เห็นตาราง)

---

## คำสั่ง / Commands

### ผู้เล่น / Player
| Command | Alias | Description | Permission |
|---------|-------|-------------|------------|
| `/gadd <name>` | `/ga` | เก็บรถที่กำลังมอง/นั่งอยู่ เข้าอู่ (อาจต้องยืนรอ ถ้าตั้ง channel) | `gadd` |
| `/gretrieve <name>` | `/gr` | เรียกรถออกจากอู่ มา spawn หน้าตัวเอง | `gretrieve` |
| `/garage` | | ดูรายชื่อรถในอู่ (แชต) | `garage` |
| `/garagedelete <name>` | | ลบรถออกจากอู่ โดยไม่ spawn | `garagedelete` |

### แอดมิน / Admin (เก็บ/เรียกทันที ไม่ติด channel)
| Command | Description | Permission |
|---------|-------------|------------|
| `/gadminadd <player/ID> <name>` | เก็บรถที่ **แอดมินมองอยู่** เข้าอู่ของผู้เล่นนั้น | `gadminadd` |
| `/gadmingive <player/ID> <VehicleID> <name>` | เพิ่มรถเข้าอู่ผู้เล่นโดยตรงจาก asset id | `gadmingive` |
| `/gadminretrieve <player/ID> <name>` | เรียกรถจากอู่ผู้เล่น มา spawn หาแอดมิน | `gadminretrieve` |

> `<player/ID>` = ชื่อผู้เล่นที่ออนไลน์ หรือ SteamID64 17 หลัก (ใช้กับผู้เล่นออฟไลน์ได้) · `<name>` ใส่เว้นวรรคได้

---

## Permissions

```xml
<!-- Rocket/Permissions.config.xml -->
<Permission Cooldown="0">gadd</Permission>
<Permission Cooldown="0">gretrieve</Permission>
<Permission Cooldown="0">garage</Permission>
<Permission Cooldown="0">garagedelete</Permission>
<Permission Cooldown="0">gadminadd</Permission>
<Permission Cooldown="0">gadmingive</Permission>
<Permission Cooldown="0">gadminretrieve</Permission>
```
แอดมินที่มี `*` มีครบทุก node อยู่แล้ว

---

## ตัวอย่างการกำหนดค่า / Configuration example

`VirtualGarage.configuration.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<VirtualGarageConfiguration xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <DatabaseHost>127.0.0.1</DatabaseHost>
  <DatabasePort>3306</DatabasePort>
  <DatabaseName>s203_unturned</DatabaseName>
  <DatabaseUsername>u203_xxxx</DatabaseUsername>
  <DatabasePassword>YOUR_PASSWORD</DatabasePassword>
  <TableName>virtual_garage</TableName>

  <MaxVehiclesPerPlayer>5</MaxVehiclesPerPlayer>
  <InteractDistance>12</InteractDistance>
  <SaveTrunkContents>true</SaveTrunkContents>
  <SaveVehicleDecorations>true</SaveVehicleDecorations>
  <AllowStoreWhileOccupied>false</AllowStoreWhileOccupied>

  <StoreChannelDefaultSeconds>0</StoreChannelDefaultSeconds>
  <StoreChannelTimes>
    <VehicleStoreTime Id="4125" Seconds="60" />
    <VehicleStoreTime Id="4000" Seconds="30" />
  </StoreChannelTimes>
  <StoreChannelSoundEffectID>56</StoreChannelSoundEffectID>
  <StoreChannelMoveCancelDistance>2</StoreChannelMoveCancelDistance>

  <ColorSuccess>green</ColorSuccess>
  <ColorError>red</ColorError>
  <ColorInfo>white</ColorInfo>

  <!-- messages use {0}/{1} placeholders; "EN | TH" -->
  <MsgStored>Stored vehicle as '{0}' | เก็บรถ '{0}' เข้าอู่แล้ว</MsgStored>
  <MsgStoreStarting>Securing vehicle... stand still for {0}s | กำลังเก็บรถ... ยืนนิ่งๆ {0} วิ</MsgStoreStarting>
  <!-- ... ข้อความอื่นๆ ถูกสร้างอัตโนมัติ ... -->
</VirtualGarageConfiguration>
```

---

## Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `StorageMode` | string | `AUTO` | `AUTO` = try MySQL, fall back to file · `MYSQL` = SQL only · `FILE` = local XML only (no SQL) |
| `DatabaseHost` | string | `127.0.0.1` | MySQL/MariaDB host |
| `DatabasePort` | ushort | `3306` | DB port |
| `DatabaseName` | string | `unturned` | Database name |
| `DatabaseUsername` | string | `root` | DB user |
| `DatabasePassword` | string | `` | DB password |
| `TableName` | string | `virtual_garage` | Table (auto‑created) |
| `MaxVehiclesPerPlayer` | int | `5` | Garage size per player |
| `InteractDistance` | float | `12` | `/gadd` raycast distance (m) |
| `SaveTrunkContents` | bool | `true` | Save/restore trunk items |
| `SaveVehicleDecorations` | bool | `true` | Save/restore mounted barricades + their contents |
| `AllowStoreWhileOccupied` | bool | `false` | Allow `/gadd` while players are inside |
| `StoreChannelDefaultSeconds` | float | `0` | Stand‑and‑wait seconds for vehicles **not** in the list (0 = instant) |
| `StoreChannelTimes` | list | one example | Per‑vehicle‑id store times (see below) |
| `StoreChannelSoundEffectID` | ushort | `56` | Sound played each second while channeling (56 = vanilla "Beep"; 0 = off) |
| `StoreChannelMoveCancelDistance` | float | `2` | Move further than this (m) from start → store cancels |
| `ColorSuccess` / `ColorError` / `ColorInfo` | string | green / red / white | Chat colours (name or `#RRGGBB`) |
| `Msg*` | string | EN \| TH | All player‑facing messages (editable) |

---

## Resource attributes

### `StoreChannelTimes` entries
```xml
<VehicleStoreTime Id="4125" Seconds="60" />
```
| Attribute | Type | Description |
|-----------|------|-------------|
| `Id` | ushort | Vehicle asset (legacy) id |
| `Seconds` | float | Seconds the player must stand and wait to store this vehicle |

ใส่ได้หลายบรรทัด · รถที่ไม่อยู่ในลิสต์ใช้ `StoreChannelDefaultSeconds`

### Database table `virtual_garage`
```
id, steam_id, name, vehicle_guid, legacy_id, skin_id, paint_color,
fuel, health, battery, tire_mask, locked, locked_owner, locked_group,
trunk_blob (base64), barricade_blob (base64), created_at
UNIQUE KEY (steam_id, name)
```

---

## Storage modes

- **`AUTO`** (default) — tries MySQL first; if it can't connect, automatically uses a local XML file
  (`Rocket/Plugins/VirtualGarage/VirtualGarage.data.xml`). ใช้ได้แม้ไม่มี SQL
- **`MYSQL`** — MySQL/MariaDB only (errors if DB is down)
- **`FILE`** — local XML file only, no SQL or `MySql.Data.dll` needed

> โหมด FILE เหมาะกับเซิร์ฟเล็ก/ทดสอบ · โหมด MySQL เหมาะกับหลายเซิร์ฟที่แชร์ฐานข้อมูล

---

## Notes

- รถจะมีข้อมูล **เฉพาะตอนอยู่ในอู่** — `/gretrieve` จะลบ row ออก (รถกลับมาเป็นรถจริง)
- ของในกระโปรง + ของในตู้บนรถ จะ**ไม่หล่นบนพื้น**ตอนเก็บ (เคลียร์ก่อนทำลายรถ) และกลับมาครบตอนเรียก
- ของตกแต่งบนรถถูกวางกลับด้วย `BarricadeManager.dropPlantedBarricade` (พิกัดโลคัลของรถ) วาง 0.5 วิ หลัง spawn เพื่อให้รถพร้อมรับ
- vehicle asset resolve ด้วย **GUID** ก่อน (กัน redirect) แล้ว fallback เป็น legacy id
- ทุกการทำงานรันบน main thread + query เล็ก/มี index → หน่วงน้อยมาก
- คำสั่งแอดมิน (`gadminadd/give/retrieve`) **เก็บ/เรียกทันที** ไม่ติด store channel

---

## Build

ต้องมี **.NET SDK** (สำหรับ `VirtualGarage.csproj` แบบ SDK‑style) หรือใช้ Visual Studio (Class Library .NET Framework 4.8)

**References (Copy Local = False ยกเว้น MySql.Data):**
- `Assembly-CSharp.dll`, `UnityEngine.dll`, `UnityEngine.CoreModule.dll`, `UnityEngine.PhysicsModule.dll`, `com.rlabrecque.steamworks.net.dll`
- `Rocket.API.dll`, `Rocket.Core.dll`, `Rocket.Unturned.dll`
- `MySql.Data.dll` (วางใน `libs/` — ดาวน์โหลดจาก NuGet 6.9.12)
- Framework: `System.Data.dll`

```powershell
dotnet build .\VirtualGarage.csproj -c Release
```

> Verified to compile clean (0 errors / 0 warnings, no deprecated API) against the real
> Unturned 3.26.3.2 + RocketMod 4.9.3.18 assemblies.

---

## License / Credits

Plugin source: free to use/modify. `MySql.Data` is © Oracle (GPLv2 + FOSS exception) — not redistributed here.
