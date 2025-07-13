# 教务系统 C# 版本 - 快速开始指南

## 🚀 快速开始

### 步骤 1: 环境准备

1. **安装 .NET 8.0 SDK**
   - 访问 [.NET 下载页面](https://dotnet.microsoft.com/download)
   - 下载并安装 .NET 8.0 SDK

2. **验证安装**
   ```powershell
   dotnet --version
   ```

### 步骤 2: 配置程序

**方法 1: 使用配置文件（推荐）**

1.  **找到配置文件**: 在 `JWSystem` 目录下，找到 `appsettings.Development.json` 文件。
2.  **编辑配置文件**: 使用文本编辑器打开该文件，填入你的个人信息：

    ```json
    {
      "UserSecrets": {
        "Username": "你的学号",
        "Password": "你的密码",
        "PushToken": "你的PushPlus Token" // 可选
      }
    }
    ```

3.  **保存文件**。

**方法 2: 临时输入**

如果配置文件中的信息为空，程序启动时会提示你手动输入学号和密码。此信息仅在当前运行期间有效。

### 步骤 3: 运行程序

**方法 1: 使用批处理脚本（推荐）**
```batch
# 双击运行或在命令行中执行
run.bat
```

**方法 2: 使用 .NET CLI**
```powershell
cd JWSystem
dotnet restore
dotnet build
dotnet run
```

## 📖 功能说明

### 1. 成绩查询 (选项 1)
- ✅ 自动登录教务系统
- ✅ 获取当前学年成绩
- ✅ 检测成绩变动
- ✅ 微信推送通知
- ✅ 本地数据缓存

### 2. 课表查询 (选项 2)
- ✅ 计算当前周数
- ✅ 获取本周课表
- ✅ 智能推送（今天/明天）
- ✅ 美化HTML格式

### 3. 考试安排查询 (选项 3)
- ✅ 获取学期考试安排
- ✅ 按日期排序
- ✅ 近期考试提醒
- ✅ 倒计时显示
- ✅ 微信推送通知

### 4. 评教系统 (选项 4)
- ✅ 自动获取待评教课程
- ✅ 自动选择A选项
- ✅ 批量提交表单
- ✅ 状态跟踪

## 🔧 配置说明

### 用户凭据配置

用户的个人信息（学号、密码、PushPlus Token）现在通过 `JWSystem/appsettings.Development.json` 文件进行管理。这是推荐的方式，因为它可以将你的敏感信息与代码库分开。

| 键 | 必需 | 说明 |
|--------|------|------|
| `Username` | ✅ | 教务系统用户名（学号） |
| `Password` | ✅ | 教务系统密码 |
| `PushToken` | ❌ | PushPlus推送Token（用于微信通知） |

### 高级配置

应用的通用设置，如教务系统URL和页面元素选择器（Selectors），存储在 `JWSystem/appsettings.json` 文件中。**通常情况下，你不需要修改此文件。**

### PushPlus 配置

1. 访问 [PushPlus官网](https://www.pushplus.plus/)
2. 使用微信扫码登录
3. 获取你的Token
4. 将获取到的Token填入 `appsettings.Development.json` 文件的 `PushToken` 字段。

## 🛠️ 故障排除

### 常见问题

1. **编译错误**
   ```
   解决：确保安装了 .NET 8.0 SDK
   验证：dotnet --version
   ```

2. **登录失败**
   ```
   检查：`appsettings.Development.json` 中的用户名和密码是否正确
   检查：网络连接是否正常
   检查：教务系统是否需要验证码
   ```

3. **推送失败**
   ```
   检查：`appsettings.Development.json` 中的 PushToken 是否正确
   检查：PushPlus服务是否正常
   ```

4. **网络错误**
   ```
   检查：是否能访问 http://jw.cupk.edu.cn
   检查：防火墙设置
   ```

### 调试模式

如需调试，可以在 Visual Studio 中：
1. 设置断点
2. 按 F5 开始调试
3. 查看变量值和执行流程

## 📁 项目文件结构

```
csharp/
├── README.md                 # 详细说明文档
├── QUICK_START.md           # 快速开始指南（本文件）
├── run.bat                  # Windows批处理启动脚本
├── setup-env.ps1           # PowerShell环境变量设置脚本
├── JWSystem.sln            # Visual Studio解决方案文件
└── JWSystem/               # 主项目目录
    ├── appsettings.json    # 应用主配置文件
    ├── appsettings.Development.json # 开发环境配置文件（用于存放用户凭据）
    ├── JWSystem.csproj     # 项目配置文件
    ├── Program.cs          # 主程序入口
    └── Services/           # 服务类目录
        ├── GradeService.cs      # 成绩查询服务
        ├── ScheduleService.cs   # 课表查询服务
        ├── ExamService.cs       # 考试安排查询服务
        └── EvaluationService.cs # 评教服务
```

## 💡 使用建议

1. **定时任务**: 可以使用Windows任务计划程序定时运行
2. **日志记录**: 建议保存运行日志以便问题排查
3. **数据备份**: 重要数据请及时备份
4. **安全性**: 请妥善保管账号密码信息

## 🤝 技术支持

如果遇到问题：
1. 查看控制台输出的错误信息
2. 检查网络连接和教务系统状态
3. 验证环境变量设置是否正确
4. 尝试重新编译和运行

## 📋 更新日志

- **v1.0.0**: 完成Python脚本的C#重写
  - 实现所有原有功能
  - 添加更好的错误处理
  - 优化代码结构和性能

---

**提示**: 首次使用建议先运行成绩查询功能测试登录是否正常。
