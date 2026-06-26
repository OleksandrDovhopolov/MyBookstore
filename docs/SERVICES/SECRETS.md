# Secrets & Credentials

Где хранить credentials, API-ключи и прочие чувствительные данные в проекте MyBookstore.

В Unity нет встроенной `.env`-конвенции (как в Node/Python), и заводить её в этом проекте мы намеренно не стали. Ниже — реально используемые паттерны и решающее правило для каждого типа секрета.

---

## Решающее правило

Перед тем как «куда-то положить» credential, ответить на два вопроса:

1. **Нужен ли этот credential клиенту (APK)?** Если нет — он вообще не должен попадать в Unity-проект.
2. **Это настоящий секрет или полу-публичный идентификатор?** Большая часть «ключей» в мобильной разработке всё равно встраивается в APK и валидируется на стороне сервиса (Firebase API key, AdMob ID, IAP license key). Их можно коммитить, но обычно `.gitignore` ставят, чтобы случайно не отдать чужой проект.

---

## Классификация credentials в проекте

| Credential | Кому нужен | Где живёт | Почему |
|---|---|---|---|
| **R2 Access Key / Secret** (запись на CDN) | Разработчику для аплоада / CI | Локально + GitHub Actions Secrets | Клиенту НЕ нужны. R2 раздаёт публично через `pub-*.r2.dev`, чтение анонимное. |
| **R2 public URL** (`pub-<hash>.r2.dev`) | Клиенту (читает каталог) | Addressables Profile (`Remote.LoadPath`) | Публичный URL, не секрет. |
| **`google-services.json`** (Firebase) | Клиенту | `Assets/.../google-services.json` + `.gitignore` | Полу-публичный, но `.gitignore` чтобы не публиковать чужой Firebase-проект. |
| **Серверные secrets** (PostgreSQL / Redis connection strings, JWT signing key) | Только серверу | Railway env vars (per environment) | Никогда не попадают в репозиторий и в APK. |
| **Config Editor Window auth** (Basic-auth для админ-API конфигов) | Только Editor (разработчику) | `EditorPrefs` (per dev machine) | Editor-only инструмент, в build не уходит. См. `CONFIG_EDITOR_WINDOW_MVP_SPEC.md`. |
| **Бэкенд URL** (`gameserver-production-be8b.up.railway.app`) | Клиенту | Захардкожено в коде | Не секрет — это публичный endpoint. |
| **Sentry/analytics DSN** (если появятся) | Клиенту | Можно коммитить в код | По дизайну публичные. |

---

## Паттерны хранения

### 1. `.gitignore` для файлов, специфичных машине

Используется для: `google-services.json`, локальных `*.local.json`, `Secrets.cs`-codegen, любых ScriptableObject с credentials.

Конвенция: рядом с секретным файлом класть `.gitignore` со звёздочкой и явно перечислять что НЕ игнорировать (README, шаблоны).

```
Assets/Secrets/
├── .gitignore          # содержит: *  и  !.gitignore  и  !README.md
├── README.md           # инструкция как сгенерировать локально
└── RuntimeSecrets.asset  ← НЕ в git
```

Этот паттерн уже применён к `google-services.json` (Firebase setup, см. PROGRESS.md → «Firebase service setup»).

### 2. `EditorPrefs` для Editor-only credentials

Используется для секретов, которые нужны только в Editor (admin-инструменты, dev-only утилиты).

```csharp
EditorPrefs.SetString("MyBookstore.Configs.BasicAuth", token);
```

Плюсы:
- Никогда не попадает в build (EditorPrefs существует только в Unity Editor).
- Per-developer per-machine — не нужно синхронизировать через git.

Минусы:
- Не передать в CI напрямую — нужен fallback на env var.

Уже используется: Config Editor Window хранит admin Basic-auth в EditorPrefs.

### 3. Environment variables + codegen в IPreprocessBuildWithReport

Используется, когда секрет нужен **в runtime билде** (попадёт в APK), но не должен лежать в git.

Скрипт перед билдом:

```csharp
public class SecretsCodegen : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        var key = Environment.GetEnvironmentVariable("FIREBASE_KEY")
                  ?? throw new BuildFailedException("FIREBASE_KEY env var not set");

        var generated = $"namespace Game.Generated {{ public static class Secrets {{ public const string FirebaseKey = \"{key}\"; }} }}";
        File.WriteAllText("Assets/Generated/Secrets.cs", generated);
        AssetDatabase.Refresh();
    }
}
```

- `Assets/Generated/Secrets.cs` — в `.gitignore`.
- На локальной машине: `setx FIREBASE_KEY ...` (один раз).
- В CI: GitHub Actions Secrets → задаётся как env перед `Unity -batchmode -build...`.

Применять только когда секрет реально критичный и нужен в runtime. Для Firebase API key, например, не нужно — он всё равно публичный.

### 4. CI Secrets (GitHub Actions / Unity Cloud Build)

Для автоматизации деплоя — единственный правильный способ.

- GitHub Actions: репозиторные `Settings → Secrets and variables → Actions`.
- Передаются в workflow через `${{ secrets.NAME }}`.
- Никогда не логируются (маскируются GitHub автоматически).

Сюда уйдут (когда подключим CI):
- `R2_ACCESS_KEY_ID` / `R2_SECRET_ACCESS_KEY` — для автоаплоада Addressables после билда.
- `ANDROID_KEYSTORE_BASE64` / `KEYSTORE_PASSWORD` — для подписи APK.
- `RAILWAY_TOKEN` (если будет деплой сервера из CI).

---

## Чего мы НЕ используем и почему

### `.env` файлы

В Unity встречается, но не идиоматично:

- Unity не парсит `.env` сам — нужен сторонний пакет ([dotenv-unity](https://github.com/acro5piano/unity-dotenv)) или самописный loader.
- Легко случайно положить в `StreamingAssets` → файл уедет в APK.
- Нет интеграции с `IPreprocessBuildWithReport` и Build Pipeline.
- Дублирует функциональность ScriptableObject + `.gitignore`, не давая ничего сверху.

Если когда-нибудь появится сильная причина (например, разработчик с фронт-бэкграундом захочет привычный workflow) — это можно сделать, но по умолчанию `.env` в этом проекте **не заводим**.

### Хардкод в коде с коммитом в git

Никогда — даже для «полу-публичных» вещей. Минимум — отдельный файл в `.gitignore`. Это страховка: если завтра ключ станет настоящим секретом или его захотят ротировать — не придётся переписывать историю git.

---

## Чек-лист при добавлении нового credential

1. **Нужен ли он клиенту?** Если нет → не клади в Unity-проект вообще. Положи в Railway env vars / GitHub Actions Secrets / 1Password.
2. **Это runtime или editor-only?**
   - Editor-only → `EditorPrefs`.
   - Runtime → ScriptableObject / codegen + `.gitignore`.
3. **Добавил в `.gitignore`?** Проверь `git status` — файла там быть НЕ должно.
4. **Положил README с инструкцией как получить/сгенерировать?** Следующий dev/CI должен знать что туда писать.
5. **Если используется в CI** — добавил в GitHub Actions Secrets и в workflow.
6. **НЕ логируется?** Грепни `Debug.Log` рядом — нельзя выводить токен/пароль даже в editor-логи (см. CONFIG_EDITOR_WINDOW_MVP_SPEC.md §«no password/Authorization leakage in logs»).

---

## Связанные документы

- `CONFIG_EDITOR_WINDOW_MVP_SPEC.md` — пример Editor-only credentials (Basic-auth для admin-API).
- `firebase-integration.md` — где живут Firebase credentials.
- `PROGRESS.md` → «Firebase service setup», «Connect PostgreSQL and Redis on Railway» — фактические решения по уже подключённым сервисам.
