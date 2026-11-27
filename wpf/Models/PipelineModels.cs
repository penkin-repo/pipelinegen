using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace PipelineGenWpf.Models
{
    public class NotifyBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }
    }

    public class ModelConfig : NotifyBase
    {
        private string _id;
        private string _name;
        private string _provider;
        private bool _isVision;
        private string _status;
        private string _statusMsg;
        private string _testedAt;

        public ModelConfig()
        {
            _id = "";
            _name = "";
            _provider = "";
            _status = "untested";
            _statusMsg = "";
            _testedAt = "";
        }

        public string Id
        {
            get { return _id; }
            set { _id = value ?? ""; OnPropertyChanged("Id"); OnPropertyChanged("DisplayLabel"); }
        }

        public string Name
        {
            get { return _name; }
            set { _name = value ?? ""; OnPropertyChanged("Name"); OnPropertyChanged("DisplayLabel"); }
        }

        public string Provider
        {
            get { return _provider; }
            set { _provider = value ?? ""; OnPropertyChanged("Provider"); OnPropertyChanged("DisplayLabel"); }
        }

        public bool IsVision
        {
            get { return _isVision; }
            set { _isVision = value; OnPropertyChanged("IsVision"); OnPropertyChanged("DisplayLabel"); }
        }

        public string Status
        {
            get { return _status; }
            set { _status = value ?? "untested"; OnPropertyChanged("Status"); }
        }

        public string StatusMsg
        {
            get { return _statusMsg; }
            set { _statusMsg = value ?? ""; OnPropertyChanged("StatusMsg"); }
        }

        public string TestedAt
        {
            get { return _testedAt; }
            set { _testedAt = value ?? ""; OnPropertyChanged("TestedAt"); }
        }

        public string DisplayLabel
        {
            get
            {
                string title = string.IsNullOrEmpty(_name) ? _id : _name;
                string visionTag = _isVision ? " · [Vision]" : "";
                string provTag = string.IsNullOrEmpty(_provider) ? "" : string.Format(" ({0})", _provider);
                return title + visionTag + provTag;
            }
        }

        public ModelConfig Clone()
        {
            return new ModelConfig
            {
                Id = this.Id,
                Name = this.Name,
                Provider = this.Provider,
                IsVision = this.IsVision,
                Status = this.Status,
                StatusMsg = this.StatusMsg,
                TestedAt = this.TestedAt
            };
        }
    }

    public class AppConfig
    {
        public AppConfig()
        {
            ApiKey = "";
            DefaultModel = "google/gemini-2.5-flash";
            Models = new List<ModelConfig>();
        }

        public string ApiKey { get; set; }
        public string DefaultModel { get; set; }
        public List<ModelConfig> Models { get; set; }
    }

    public class StepDefinition : NotifyBase
    {
        private string _id;
        private string _type;
        private string _title;
        private string _content;
        private string _fileName;
        private string _fileData;
        private string _fileType;
        private string _model;
        private string _prompt;
        private string _result;
        private string _status;
        private bool _requireUser;
        private string _userPrompt;
        private string _instruction;

        public StepDefinition()
        {
            _id = "";
            _type = "text";
            _title = "";
            _content = "";
            _fileName = "";
            _fileData = "";
            _fileType = "";
            _model = "";
            _prompt = "";
            _result = "";
            _status = "idle";
            _requireUser = false;
            _userPrompt = "";
            _instruction = "";
        }

        public string Id
        {
            get { return _id; }
            set { _id = value ?? ""; OnPropertyChanged("Id"); }
        }

        public string Type
        {
            get { return _type; }
            set { _type = value ?? "text"; OnPropertyChanged("Type"); }
        }

        public string Title
        {
            get { return _title; }
            set { _title = value ?? ""; OnPropertyChanged("Title"); }
        }

        public string Content
        {
            get { return _content; }
            set { _content = value ?? ""; OnPropertyChanged("Content"); }
        }

        public string FileName
        {
            get { return _fileName; }
            set { _fileName = value ?? ""; OnPropertyChanged("FileName"); }
        }

        public string FileData
        {
            get { return _fileData; }
            set { _fileData = value ?? ""; OnPropertyChanged("FileData"); }
        }

        public string FileType
        {
            get { return _fileType; }
            set { _fileType = value ?? ""; OnPropertyChanged("FileType"); }
        }

        public string Model
        {
            get { return _model; }
            set { _model = value ?? ""; OnPropertyChanged("Model"); }
        }

        public string Prompt
        {
            get { return _prompt; }
            set { _prompt = value ?? ""; OnPropertyChanged("Prompt"); }
        }

        public string Result
        {
            get { return _result; }
            set { _result = value ?? ""; OnPropertyChanged("Result"); }
        }

        public string Status
        {
            get { return _status; }
            set { _status = value ?? "idle"; OnPropertyChanged("Status"); }
        }

        public bool RequireUser
        {
            get { return _requireUser; }
            set { _requireUser = value; OnPropertyChanged("RequireUser"); }
        }

        public string UserPrompt
        {
            get { return _userPrompt; }
            set { _userPrompt = value ?? ""; OnPropertyChanged("UserPrompt"); }
        }

        public string Instruction
        {
            get { return _instruction; }
            set { _instruction = value ?? ""; OnPropertyChanged("Instruction"); }
        }
    }

    public class Project : NotifyBase
    {
        private string _id;
        private string _name;
        private string _description;
        private string _updatedAt;
        private string _createdAt;
        private List<StepDefinition> _steps;

        public Project()
        {
            _id = "";
            _name = "";
            _description = "";
            _updatedAt = "";
            _createdAt = "";
            _steps = new List<StepDefinition>();
        }

        public string Id
        {
            get { return _id; }
            set { _id = value ?? ""; OnPropertyChanged("Id"); }
        }

        public string Name
        {
            get { return _name; }
            set { _name = value ?? ""; OnPropertyChanged("Name"); }
        }

        public string Description
        {
            get { return _description; }
            set { _description = value ?? ""; OnPropertyChanged("Description"); }
        }

        public string UpdatedAt
        {
            get { return _updatedAt; }
            set { _updatedAt = value ?? ""; OnPropertyChanged("UpdatedAt"); OnPropertyChanged("Subtitle"); }
        }

        public string CreatedAt
        {
            get { return _createdAt; }
            set { _createdAt = value ?? ""; OnPropertyChanged("CreatedAt"); }
        }

        public List<StepDefinition> Steps
        {
            get { return _steps; }
            set { _steps = value ?? new List<StepDefinition>(); OnPropertyChanged("Steps"); OnPropertyChanged("Subtitle"); }
        }

        public string Subtitle
        {
            get
            {
                int count = _steps != null ? _steps.Count : 0;
                string updated = string.IsNullOrEmpty(_updatedAt) ? "сейчас" : _updatedAt;
                return string.Format("{0} шагов · {1}", count, updated);
            }
        }

        public void NotifyMetaChanged()
        {
            OnPropertyChanged("Name");
            OnPropertyChanged("UpdatedAt");
            OnPropertyChanged("Subtitle");
        }
    }

    public class LogEntry
    {
        public string Id { get; set; }
        public string Time { get; set; }
        public string Type { get; set; } // "info", "ok", "error"
        public string Message { get; set; }
    }

    public class LoadedFileInfo
    {
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public string FileType { get; set; }
        public bool IsImage { get; set; }
        public string FileData { get; set; }
        public string Content { get; set; }
    }

    public class CatalogModelItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsVision { get; set; }
    }

    public class ChatMessageItem
    {
        public string Role { get; set; }
        public object Content { get; set; } // string or List<Dictionary<string, object>>
    }
}
