# JWSystem - 教务系统自动化工具

[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)]()

一个基于 .NET 8.0 开发的教务系统自动化工具，支持成绩查询、课表查询、考试安排查询和自动评教等功能。

## ✨ 主要功能

| 功能模块 | 描述 | 原Python文件 |
|---------|------|-------------|
| 🎯 **成绩查询** | 自动查询成绩变动并推送微信通知 | `cjcx.py` |
| 📅 **课表查询** | 查询当前周课表并智能推送 | `jw.py` |  
| 📝 **考试安排** | 查询考试时间并提醒近期考试 | `kstx.py` |
| ⭐ **自动评教** | 一键完成所有课程评教 | `pj.py` |

## 🔧 系统要求

- **操作系统**: Windows 10/11 (x64)
- **运行时**: .NET 8.0 Runtime 或更高版本
- **开发环境**: Visual Studio 2022 或 .NET 8.0 SDK

## 🚀 快速开始

### 方式一：使用预编译版本（推荐）

1. 从 [Releases](../../releases) 页面下载最新版本的安装包
2. 运行 `JWSystem-Setup-v1.0.exe` 进行安装
3. 配置账号信息（见下方配置说明）
4. 启动程序开始使用

### 方式二：从源码运行

1. **克隆仓库**
   ```bash
   git clone https://github.com/your-username/JWSystem.git
   cd JWSystem
   ```

2. **安装 .NET 8.0 SDK**
   
   从 [Microsoft官网](https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0) 下载并安装

3. **配置应用设置**
   
   编辑 `JWSystem/appsettings.Development.json` 文件：
   ```json
   {
     "UserSecrets": {
       "Username": "你的学号",
       "Password": "你的密码", 
       "PushToken": "PushPlus推送token（可选）"
     }
   }
   ```

4. **运行程序**
   ```bash
   # 使用批处理脚本（推荐）
   run.bat
   
   # 或直接使用 dotnet 命令
   cd JWSystem
   dotnet run
   ```

## ⚙️ 详细配置

### 🔧 初次配置
项目使用两个配置文件：
- `appsettings.json` - 系统配置（无需修改）
- `appsettings.Development.json` - 用户敏感信息配置

### 🔑 用户账号配置
编辑 `JWSystem/appsettings.Development.json` 文件：

```json
{
  "UserSecrets": {
    "Username": "你的学号",
    "Password": "你的密码",
    "PushToken": "你的PushPlus Token（可选）"
  }
}
```

### 📋 配置说明
- `Username`: 教务系统登录用户名（学号）
- `Password`: 教务系统登录密码  
- `PushToken`: PushPlus微信推送Token（可选，用于接收通知）

### 📱 PushPlus配置（可选）
1. 访问 [PushPlus官网](http://www.pushplus.plus/) 注册账号
2. 获取你的Token
3. 将Token填入 `appsettings.Development.json` 的 `PushToken` 字段

> 💡 **安全提示**: 敏感信息存储在 `appsettings.Development.json` 中，该文件已在 `.gitignore` 中排除，不会被提交到版本控制系统。

## 📱 功能详解

### 🎯 成绩查询系统
- **自动登录**: 使用配置的账号密码自动登录教务系统
- **成绩监控**: 获取当前学年成绩并与历史记录对比
- **变动通知**: 检测到成绩变化时自动推送微信通知
- **数据缓存**: 本地保存成绩数据，避免重复推送

### 📅 课表查询系统  
- **智能周数**: 自动计算当前学周
- **课程解析**: 提取课程名称、时间、教室、教师等信息
- **时间推送**: 根据当前时间智能推送今日或明日课表
- **美化展示**: HTML格式的精美课表推送

### 📝 考试安排查询
- **学期选择**: 自动获取可用学期并查询考试安排
- **时间排序**: 按考试时间排序显示
- **近期提醒**: 突出显示一周内的考试安排
- **详细信息**: 包含考试时间、地点、座位号等完整信息

### ⭐ 自动评教系统
- **一键评教**: 自动为所有未评教课程进行评教
- **智能选择**: 默认选择A选项（优秀）
- **批量处理**: 支持多门课程同时评教
- **安全可靠**: 模拟真实用户操作，避免异常

## 🛠️ 项目结构

```
JWSystem/
├── JWSystem/                      # 主项目目录
│   ├── Models/                    # 数据模型
│   │   └── AppSettings.cs         # 配置模型
│   ├── Services/                  # 业务服务
│   │   ├── GradeService.cs        # 成绩查询服务
│   │   ├── ScheduleService.cs     # 课表查询服务
│   │   ├── ExamService.cs         # 考试安排服务
│   │   └── EvaluationService.cs   # 评教服务
│   ├── Program.cs                 # 程序入口
│   ├── appsettings.json           # 系统配置文件
│   ├── appsettings.Development.json # 用户配置文件
│   └── JWSystem.csproj            # 项目文件
├── create_installer.iss           # Inno Setup安装脚本
├── run.bat                        # Windows运行脚本
├── .gitignore                     # Git忽略文件列表
├── LICENSE                        # MIT开源许可证
├── README.md                      # 项目说明
├── QUICK_START.md                 # 快速开始指南
└── JWSystem.sln                   # VS解决方案文件
```

## 🔒 安全说明

- ✅ **本地存储**: 所有账号密码仅存储在本地配置文件中
- ✅ **开源透明**: 完全开源，可审查所有代码逻辑  
- ✅ **无后门**: 不向任何第三方服务器发送敏感信息
- ✅ **加密传输**: 与教务系统的通信使用HTTPS加密

## 🤝 贡献指南

欢迎提交 Issue 和 Pull Request！

1. Fork 本仓库
2. 创建你的功能分支 (`git checkout -b feature/AmazingFeature`)
3. 提交你的修改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 开启一个 Pull Request

## 📄 许可证

本项目基于 MIT 许可证开源 - 查看 [LICENSE](LICENSE) 文件了解详情。

## ⚠️ 免责声明

本工具仅供学习和个人使用，请遵守学校相关规定。使用本工具所产生的任何后果由用户自行承担。

## 🔗 相关链接

- [PushPlus 微信推送服务](http://www.pushplus.plus/)
- [.NET 8.0 下载](https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0)
- [Inno Setup 安装包制作工具](https://jrsoftware.org/isinfo.php)

---

如果这个项目对你有帮助，请给个 ⭐ Star 支持一下！
