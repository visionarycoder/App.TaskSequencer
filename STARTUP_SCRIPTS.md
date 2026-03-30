# App.TaskSequencer - Startup Scripts

Quick-start batch scripts to launch the application components.

## 📋 Available Scripts

Located in the solution root: `c:\repos\vc\TaskSequencer\`

### 🚀 **start-all.bat** (RECOMMENDED)
**Start everything in orchestrated mode**

```batch
start-all.bat
```

**What happens:**
- ✓ Starts Aspire.AppHost (orchestrator)
- ✓ Aspire launches Orleans silo (localhost:11111)
- ✓ Aspire launches Console client (connects and generates plans)
- ✓ Optionally starts MAUI desktop app
- ✓ Shows Aspire dashboard at http://localhost:15000

**Use case:** First-time users, full system integration testing

---

### 🏛️ **start-aspire.bat**
**Start Aspire orchestration only**

```batch
start-aspire.bat
```

**What happens:**
- ✓ Starts Aspire.AppHost
- ✓ Launches Orleans silo
- ✓ Launches Console client
- ✓ Aspire dashboard at http://localhost:15000

**Use case:** Testing orchestrated services without MAUI

---

### 🧬 **start-orleans.bat**
**Start Orleans silo only**

```batch
start-orleans.bat
```

**What happens:**
- ✓ Starts Orleans silo on localhost:11111
- ✓ Ready for clients to connect

**Use case:** Manual testing, running clients separately

---

### 🖥️ **start-console.bat**
**Start Console client only**

```batch
start-console.bat
```

**Prerequisites:**
- Orleans silo must be running (start-orleans.bat or start-aspire.bat)

**What happens:**
- ✓ Connects to Orleans silo (localhost:11111)
- ✓ Auto-retries connection if silo starting up
- ✓ Generates execution plans using Orleans grains

**Use case:** Testing console functionality independently

---

### 🎨 **start-maui.bat**
**Start MAUI desktop client only**

```batch
start-maui.bat
```

**Prerequisites:**
- Orleans silo must be running (start-orleans.bat or start-aspire.bat)

**What happens:**
- ✓ Launches MAUI Windows desktop app
- ✓ Connects to Orleans silo (localhost:11111)
- ✓ Auto-retries connection

**Use case:** Testing UI independently, development

---

## 🔄 Startup Sequences

### **Option 1: Full System (Recommended for First Run)**
```
Double-click: start-all.bat
```
Automatically starts everything in correct order.

---

### **Option 2: Manual Control**

**Terminal 1:**
```
start-orleans.bat
```

**Terminal 2:**
```
start-console.bat
```

**Terminal 3 (optional):**
```
start-maui.bat
```

---

### **Option 3: Aspire Orchestration**

**Single terminal:**
```
start-aspire.bat
```
Handles startup order automatically.

---

## 🔍 Troubleshooting

### Script won't run?
- Ensure you're in the solution root: `c:\repos\vc\TaskSequencer\`
- Right-click → "Run as Administrator" if permission errors

### Orleans connection failed?
- Ensure Orleans silo is running first
- Check that port 11111 isn't in use: `netstat -ano | findstr :11111`
- Try increasing retry timeout in client app

### "dotnet not found"?
- Ensure .NET 10 SDK is installed
- Add dotnet to PATH or use full path to dotnet.exe

---

## 📊 Service Architecture

```
start-all.bat
  ├── Aspire.AppHost
  │   ├── Start Orleans Silo (localhost:11111)
  │   └── Start Console Client (auto-connects)
  └── [Optional] Start MAUI Client
```

---

## 🌐 Dashboard & Monitoring

- **Aspire Dashboard:** http://localhost:15000 (when using start-all.bat or start-aspire.bat)
- Shows service status, logs, traces, and health checks

---

## ⚙️ Configuration

Scripts use default configuration:
- Orleans port: `11111`
- Aspire port: `15000`
- Retry attempts: `10` (with 1-second delays)
- Target framework: `net10.0-windows10.0.20348`

To modify, edit the corresponding `Program.cs` or batch script.

---

## 💡 Tips

1. **Keep scripts running** - They print logs to console, close = stop service
2. **Multiple terminals** - Each `start cmd /k` opens a new window
3. **Development loop** - Use `start-orleans.bat` in one terminal, restart clients in others
4. **Debugging** - Set breakpoints in Visual Studio, then run scripts manually
