# Event Bus

**Type-safe event bus for Unity 6**: let systems talk to each other without references. The player
dies → UI, audio, score and achievements react, and the player script knows none of them.

[English](#english) · [Türkçe](#türkçe)

---

<a name="english"></a>
# English

Two tools in one package. Use whichever fits, or both:

| | **EventBus** (code) | **Event Channels** (ScriptableObject assets) |
|---|---|---|
| Wiring | `Publish` / `Subscribe` by type | Drag the same asset into the sender and the listener |
| Best for | Programmers, many event types, struct payloads | Designers, Inspector wiring, UnityEvents, no code |
| Example | `EventBus.Publish(new PlayerDied { … })` | `onPlayerDied.Raise()` + a **Void Event Listener** |

Both are safe in the ways that usually bite:

- Subscribing or unsubscribing **while an event is being published** is safe. Removed listeners are
  skipped right away; new ones start with the next publish.
- One listener throwing an exception **does not stop the others**. The exception is still logged.
- A listener whose **GameObject was destroyed** is removed automatically (no `MissingReferenceException`
  from a forgotten `Unsubscribe`).
- Works with **Enter Play Mode Options** (domain reload off): nothing leaks between play sessions.
- **Event Monitor** window shows every publish live, how many listeners it reached, and highlights
  events nobody heard.

**Requirements:** Unity 6 (6000.0+). No other packages.

## Step-by-step setup

### 1. Add the package

**Window > Package Manager** → **+** → **Install package from git URL…** →

```
https://github.com/Atthree/EventBus.git
```

To lock a version: `https://github.com/Atthree/EventBus.git#v0.1.0`

### 2. EventBus from code

**a) Define an event.** Any type works; a small `struct` is the usual choice (no garbage):

```csharp
public struct PlayerDied
{
    public Vector3 position;
    public GameObject killer;
}
```

**b) Listen.** Inside a MonoBehaviour, use `this.Subscribe`. The subscription ends by itself when
the object is destroyed, so you don't need `OnDestroy`:

```csharp
using Atthree.Events;

public class GameOverScreen : MonoBehaviour
{
    void Awake()
    {
        this.Subscribe<PlayerDied>(e => Show(e.killer));
    }
}
```

**c) Publish** from anywhere:

```csharp
EventBus.Publish(new PlayerDied { position = transform.position, killer = lastAttacker });
```

**Listening only while enabled** (common for UI): keep the token and dispose it.

```csharp
Subscription sub;
void OnEnable()  => sub = EventBus.Subscribe<ScoreChanged>(OnScore);
void OnDisable() => sub.Dispose();
```

Other calls:

| Call | What it does |
|---|---|
| `EventBus.Subscribe<T>(handler)` | Listen. Returns a `Subscription` token. Adding the same method twice counts once. |
| `EventBus.Subscribe<T>(handler, owner)` | Listen until `owner` is destroyed. |
| `this.Subscribe<T>(handler)` | Same, with `this` as the owner. |
| `EventBus.SubscribeOnce<T>(handler)` | Only the next publish, then stop. |
| `EventBus.Unsubscribe<T>(handler)` | Stop (method groups; for lambdas keep the token). |
| `EventBus.UnsubscribeAll(owner)` | Drop everything this owner subscribed to. |
| `EventBus.Publish(evt)` | Send to all listeners of `evt`'s type. |
| `EventBus.SubscriberCount<T>()` | How many listeners right now. |
| `EventBus.Clear()` | Remove every subscription (tests, full resets). |

### 3. Event Channels (no code)

**a) Create a channel asset:** Project window → **Create > Atthree > Events > Void Channel**
(or Int / Float / Bool / String / Vector3 / GameObject). Name it after the event, for example
`OnPlayerDied`.

**b) Raise it.** In code, keep a reference and call `Raise`:

```csharp
[SerializeField] VoidEventChannel onPlayerDied;
...
onPlayerDied.Raise();
```

Or from a UnityEvent in the Inspector (a Button's **On Click**, for example): drag the channel asset in
and pick **VoidEventChannel > Raise**.

**c) React to it:** on any GameObject, **Add Component > Atthree > Events > Void Event Listener**,
drag the same channel into **Channel**, and fill **Response** like any UnityEvent (enable a panel, play
a sound, call a method). The listener only reacts while its GameObject is active.

**d) Test it:** in Play mode, select the channel asset. The Inspector shows the listener count and a
**Raise** button that sends the **Debug Value**.

**Your own payload type:**

```csharp
[CreateAssetMenu(menuName = "Game/Events/Damage Channel")]
public class DamageChannel : EventChannel<DamageData> { }

public class DamageListener : EventChannelListener<DamageData, DamageChannel> { }
```

`DamageData` must be `[System.Serializable]` if you want to edit its debug value in the Inspector.
Each class has to live in its own file named after the class (Unity's rule for ScriptableObjects and
MonoBehaviours).

### 4. Event Monitor

**Window > Atthree > Event Monitor.** In Play mode it lists every `EventBus.Publish` and every channel
`Raise`: frame, time, source, type, payload and how many listeners heard it. Rows with **0 listeners**
are orange: the usual answer to "why did nothing happen?". Click a channel row to ping the asset.
The bottom section shows live subscriber counts per event type. Recording only runs while the window is
open.

## Which one should I use?

- **Code-heavy systems, lots of event types, data-rich events:** EventBus.
- **Things a designer wires up** (button → open door, boss died → play cutscene): channels.
- **Mixing is fine.** A common setup: gameplay code publishes on the EventBus; a small bridge script
  raises a channel for the few events designers need.

| Game type | Typical events |
|---|---|
| Action / shooter | `PlayerDamaged`, `EnemyKilled`, `WeaponFired`, `AmmoChanged` |
| Platformer | `CoinCollected`, `CheckpointReached`, `PlayerRespawned` |
| Puzzle | `PieceMoved`, `PuzzleSolved`, `MoveUndone` |
| Strategy / sim | `ResourceChanged`, `BuildingPlaced`, `DayEnded` |
| Any | `GamePaused`, `SceneLoaded`, `SettingsChanged`, `QuestCompleted` |

## Good to know

- **Events are matched by exact type.** Publishing a `BossDied` does not reach `Subscribe<EnemyDied>`
  listeners, even if `BossDied` inherits from `EnemyDied`. Use one event type with a field
  (`isBoss`) or publish both.
- **Lambdas and `Unsubscribe`:** `EventBus.Unsubscribe(x => …)` cannot remove a lambda, because every
  lambda is a new object. Use `this.Subscribe` (auto cleanup) or keep the `Subscription` token.
- **Auto cleanup happens at the next publish.** A destroyed object's listener is removed when its event
  type is next published, so `SubscriberCount` can briefly include it. It is never called.
- **Disabled is not destroyed.** A disabled MonoBehaviour still receives EventBus events. If you want
  "only while enabled", subscribe in `OnEnable` and dispose in `OnDisable` (see above). Channel
  *listener components* already behave this way.
- **Order:** listeners run in the order they subscribed. Don't build logic that depends on it.
- **Recursion:** publishing the same event from inside its own listener works, but it is easy to make an
  infinite loop. Avoid it.
- **Threads:** call from the main thread only (like the rest of Unity's API).
- **Channel assets keep state during Play mode** (`LastValue`, listeners) and are reset when you exit
  Play mode, so nothing carries over between runs.

## Troubleshooting

| Problem | Cause and fix |
|---|---|
| Nothing happens on publish | Open the Event Monitor. Orange row = no listeners: the listener subscribed too late, already unsubscribed, or subscribed to a different type (check namespaces: two `PlayerDied` types?). |
| Listener runs twice | Two different lambdas subscribed (lambdas are never deduplicated), or two components. |
| `MissingReferenceException` inside a listener | The listener touches another object that was destroyed. The bus only removes listeners whose *own* owner is destroyed. |
| Channel listener doesn't react | Its GameObject or component is disabled, or the **Channel** field points to a different asset (two assets with the same name?). |
| Can't create a custom channel asset | The class must not be generic or abstract, and the file name must match the class name. |

## How it works

- One channel per event type (`Dictionary<Type, Channel<T>>`). Publishing a type nobody listens to is
  a single dictionary lookup.
- Subscriptions are stored in a **copy-on-write array**: subscribing or unsubscribing builds a new array,
  and `Publish` walks the array it read at the start. That makes changes during a publish safe without
  locks or extra allocations per publish.
- Each subscription has an `active` flag; removing it clears the flag, so an in-flight publish skips it.
- If you don't pass an owner but the handler is a method on a `UnityEngine.Object`, that object
  becomes the owner automatically.
- Static state is reset in `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` and channel assets
  clear their listeners when the Editor returns to Edit mode.

## Tests

19 PlayMode tests: payload delivery, type isolation, deduplication, unsubscribe and token disposal,
exception isolation, unsubscribe/subscribe during publish, `SubscribeOnce`, destroyed-owner cleanup,
`UnsubscribeAll`, the debug hook, and channel raise, listener enable/disable and channel swapping.

To run them in your project, add to `Packages/manifest.json`:

```json
"testables": [ "com.atthree.event-bus" ]
```

then **Window > General > Test Runner > PlayMode > Run All**.

## License

[MIT](LICENSE.md)

---

<a name="türkçe"></a>
# Türkçe

Sistemler birbirine referans vermeden haberleşir. Oyuncu ölür → arayüz, ses, skor ve başarımlar
tepki verir; oyuncu script'i bunların hiçbirini tanımaz.

İki araç tek pakette, hangisi işine yararsa (ya da ikisi birden):

| | **EventBus** (kod) | **Event Channel** (ScriptableObject asset) |
|---|---|---|
| Bağlantı | Tipe göre `Publish` / `Subscribe` | Aynı asset'i yayınlayana ve dinleyene sürükle |
| En uygun | Programcı, çok sayıda olay tipi, veri taşıyan struct'lar | Tasarımcı, Inspector bağlantısı, UnityEvent, kodsuz |
| Örnek | `EventBus.Publish(new PlayerDied { … })` | `onPlayerDied.Raise()` + **Void Event Listener** |

Genelde can sıkan durumlara karşı güvenli:

- Yayın **sürerken** abone olmak / çıkmak güvenli. Çıkan dinleyici hemen atlanır, yeni eklenen bir
  sonraki yayından itibaren çalışır.
- Bir dinleyicinin hatası **diğerlerini durdurmaz**; hata yine Console'a yazılır.
- **Yok edilmiş** bir objenin dinleyicisi otomatik silinir (unutulan `Unsubscribe` yüzünden
  `MissingReferenceException` yok).
- **Enter Play Mode Options** (domain reload kapalı) ile çalışır: Play'ler arası sızıntı yok.
- **Event Monitor** penceresi her yayını canlı gösterir, kaç dinleyiciye ulaştığını yazar,
  kimsenin duymadığı olayları turuncu yapar.

**Gereksinimler:** Unity 6 (6000.0+). Başka paket gerekmez.

## Adım adım kurulum

### 1. Paketi ekle

**Window > Package Manager** → **+** → **Install package from git URL…** →

```
https://github.com/Atthree/EventBus.git
```

Belirli bir sürüm için: `https://github.com/Atthree/EventBus.git#v0.1.0`

### 2. Koddan EventBus

**a) Olayı tanımla.** Her tip olur; genelde küçük bir `struct` (çöp üretmez):

```csharp
public struct PlayerDied
{
    public Vector3 position;
    public GameObject killer;
}
```

**b) Dinle.** MonoBehaviour içinde `this.Subscribe` kullan. Obje yok olunca abonelik kendiliğinden
biter, `OnDestroy` yazmana gerek yok:

```csharp
using Atthree.Events;

public class GameOverScreen : MonoBehaviour
{
    void Awake()
    {
        this.Subscribe<PlayerDied>(e => Show(e.killer));
    }
}
```

**c) Yayınla**, herhangi bir yerden:

```csharp
EventBus.Publish(new PlayerDied { position = transform.position, killer = lastAttacker });
```

**Sadece açıkken dinlemek** (arayüzde sık): jetonu tut, Dispose et.

```csharp
Subscription sub;
void OnEnable()  => sub = EventBus.Subscribe<ScoreChanged>(OnScore);
void OnDisable() => sub.Dispose();
```

Diğer çağrılar:

| Çağrı | Ne yapar |
|---|---|
| `EventBus.Subscribe<T>(handler)` | Dinle. `Subscription` jetonu döner. Aynı metodu iki kez eklemek tek sayılır. |
| `EventBus.Subscribe<T>(handler, owner)` | `owner` yok olana kadar dinle. |
| `this.Subscribe<T>(handler)` | Aynısı, sahip `this`. |
| `EventBus.SubscribeOnce<T>(handler)` | Sadece bir sonraki yayın, sonra bırak. |
| `EventBus.Unsubscribe<T>(handler)` | Bırak (metot için; lambda için jetonu tut). |
| `EventBus.UnsubscribeAll(owner)` | Bu sahibin tüm aboneliklerini bırak. |
| `EventBus.Publish(evt)` | `evt`'nin tipini dinleyen herkese gönder. |
| `EventBus.SubscriberCount<T>()` | Şu an kaç dinleyici var. |
| `EventBus.Clear()` | Tüm abonelikleri sil (test, tam sıfırlama). |

### 3. Event Channel (kodsuz)

**a) Kanal asset'i oluştur:** Project penceresi → **Create > Atthree > Events > Void Channel**
(ya da Int / Float / Bool / String / Vector3 / GameObject). Olayın adını ver, örneğin `OnPlayerDied`.

**b) Yayınla.** Kodda referans tut ve `Raise` çağır:

```csharp
[SerializeField] VoidEventChannel onPlayerDied;
...
onPlayerDied.Raise();
```

Ya da Inspector'daki bir UnityEvent'ten (örneğin Button'ın **On Click**'i): kanal asset'ini sürükle,
**VoidEventChannel > Raise** seç.

**c) Tepki ver:** herhangi bir objeye **Add Component > Atthree > Events > Void Event Listener** ekle,
aynı kanalı **Channel**'a sürükle, **Response**'u normal bir UnityEvent gibi doldur (panel aç, ses çal,
metot çağır). Dinleyici sadece objesi aktifken tepki verir.

**d) Test et:** Play modunda kanal asset'ini seç. Inspector dinleyici sayısını ve **Debug Value**'yu
gönderen bir **Raise** düğmesini gösterir.

**Kendi veri tipin:**

```csharp
[CreateAssetMenu(menuName = "Game/Events/Damage Channel")]
public class DamageChannel : EventChannel<DamageData> { }

public class DamageListener : EventChannelListener<DamageData, DamageChannel> { }
```

Debug değerini Inspector'dan düzenlemek istersen `DamageData` `[System.Serializable]` olmalı.
Her sınıf, sınıf adıyla aynı isimli kendi dosyasında olmalı (Unity'nin ScriptableObject ve
MonoBehaviour kuralı).

### 4. Event Monitor

**Window > Atthree > Event Monitor.** Play modunda her `EventBus.Publish` ve her kanal `Raise`'i
listeler: kare, zaman, kaynak, tip, içerik ve kaç dinleyicinin duyduğu. **0 dinleyicili** satırlar
turuncu: "neden hiçbir şey olmadı?" sorusunun cevabı genelde budur. Kanal satırına tıklamak asset'i
gösterir. Alt bölümde olay tipi başına canlı abone sayıları var. Kayıt sadece pencere açıkken tutulur.

## Hangisini kullanmalıyım?

- **Kod ağırlıklı sistemler, çok olay tipi, veri taşıyan olaylar:** EventBus.
- **Tasarımcının bağlayacağı şeyler** (buton → kapı aç, boss öldü → ara sahne): kanallar.
- **Karıştırmak sorun değil.** Yaygın kurulum: oyun kodu EventBus'a yayınlar; küçük bir köprü script'i
  tasarımcıya lazım olan birkaç olay için kanal yayınlar.

| Oyun türü | Tipik olaylar |
|---|---|
| Aksiyon / nişancı | `PlayerDamaged`, `EnemyKilled`, `WeaponFired`, `AmmoChanged` |
| Platform | `CoinCollected`, `CheckpointReached`, `PlayerRespawned` |
| Bulmaca | `PieceMoved`, `PuzzleSolved`, `MoveUndone` |
| Strateji / simülasyon | `ResourceChanged`, `BuildingPlaced`, `DayEnded` |
| Hepsi | `GamePaused`, `SceneLoaded`, `SettingsChanged`, `QuestCompleted` |

## Dikkat edilecekler

- **Olaylar tam tipe göre eşleşir.** `BossDied` yayınlamak, `BossDied` `EnemyDied`'dan türese bile
  `Subscribe<EnemyDied>` dinleyicilerine ulaşmaz. Tek tip + alan (`isBoss`) kullan ya da ikisini de yayınla.
- **Lambda ve `Unsubscribe`:** `EventBus.Unsubscribe(x => …)` lambda'yı silemez, her lambda yeni bir
  objedir. `this.Subscribe` (otomatik temizlik) kullan ya da `Subscription` jetonunu tut.
- **Otomatik temizlik bir sonraki yayında olur.** Yok edilmiş objenin dinleyicisi o tip bir daha
  yayınlanınca silinir; bu arada `SubscriberCount` onu sayabilir. Ama asla çağrılmaz.
- **Kapalı ≠ yok edilmiş.** Kapalı (disabled) bir MonoBehaviour EventBus olaylarını almaya devam eder.
  "Sadece açıkken" istiyorsan `OnEnable`'da abone ol, `OnDisable`'da Dispose et (yukarıda).
  Kanal *listener bileşenleri* zaten böyle çalışır.
- **Sıra:** dinleyiciler abone oldukları sırayla çalışır. Bu sıraya bağlı mantık kurma.
- **Özyineleme:** bir olayı kendi dinleyicisinin içinden yayınlamak çalışır ama sonsuz döngü kurmak
  çok kolaydır. Kaçın.
- **Thread:** sadece ana thread'den çağır (Unity API'sinin geri kalanı gibi).
- **Kanal asset'leri Play boyunca durum tutar** (`LastValue`, dinleyiciler) ve Play'den çıkınca
  sıfırlanır; çalıştırmalar arasında bir şey taşınmaz.

## Sorun giderme

| Sorun | Sebep ve çözüm |
|---|---|
| Yayınlıyorum, bir şey olmuyor | Event Monitor'ü aç. Turuncu satır = dinleyici yok: dinleyici geç abone olmuş, çoktan çıkmış ya da farklı bir tipe abone (namespace'lere bak: iki ayrı `PlayerDied` mı var?). |
| Dinleyici iki kez çalışıyor | İki farklı lambda abone olmuş (lambdalar ayıklanmaz) ya da iki bileşen var. |
| Dinleyicinin içinde `MissingReferenceException` | Dinleyici yok edilmiş *başka* bir objeye dokunuyor. Otobüs sadece *kendi* sahibi yok olan dinleyicileri siler. |
| Kanal dinleyicisi tepki vermiyor | Objesi ya da bileşeni kapalı, ya da **Channel** başka bir asset'i gösteriyor (aynı isimli iki asset?). |
| Kendi kanal asset'imi oluşturamıyorum | Sınıf generic ya da abstract olmamalı, dosya adı sınıf adıyla aynı olmalı. |

## Nasıl çalışıyor?

- Her olay tipi için bir kanal (`Dictionary<Type, Channel<T>>`). Kimsenin dinlemediği bir tipi
  yayınlamak tek bir sözlük aramasıdır.
- Abonelikler **kopyala-yaz dizide** tutulur: abone olmak / çıkmak yeni dizi kurar, `Publish` başta
  okuduğu diziyi gezer. Böylece yayın sırasında değişiklik kilitsiz ve yayın başına ek bellek ayırmadan
  güvenlidir.
- Her aboneliğin bir `active` bayrağı var; silinince bayrak iner, süren yayın onu atlar.
- Sahip vermediysen ama metot bir `UnityEngine.Object`'e aitse, o obje otomatik sahip olur.
- Statik durum `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` ile sıfırlanır; kanal asset'leri
  Editor Edit moda dönünce dinleyicilerini temizler.

## Testler

19 PlayMode testi. Projende çalıştırmak için `Packages/manifest.json`'a ekle:

```json
"testables": [ "com.atthree.event-bus" ]
```

sonra **Window > General > Test Runner > PlayMode > Run All**.

## Lisans

[MIT](LICENSE.md)
