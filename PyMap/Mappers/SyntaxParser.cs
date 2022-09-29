using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PyMap
{
    using System.ComponentModel;
    using System.IO;

    class SyntaxParser : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public event Action MapInvalidated;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if (propertyName != nameof(ErrorMessage))
                MapInvalidated?.Invoke();
        }

        public ObservableCollection<MemberInfo> MemberList { get; set; } = new ObservableCollection<MemberInfo>();

        bool privateFields = true;
        bool publicFields = true;
        bool privateProperties = true;
        bool publicProperties = true;
        bool publicMethods = true;
        bool privateMethods = true;
        bool sortMembers = true;
        string className;
        string memberName;

        public bool SortMembers
        {
            get => sortMembers; set { sortMembers = value; OnPropertyChanged(nameof(SortMembers)); }
        }

        public string MemberName
        {
            get => memberName; set { memberName = value; OnPropertyChanged(nameof(MemberName)); }
        }

        public string ClassName
        {
            get => className; set { className = value; OnPropertyChanged(nameof(ClassName)); }
        }

        public bool PublicFields
        {
            get => publicFields; set { publicFields = value; OnPropertyChanged(nameof(PublicFields)); }
        }

        public bool PrivateProperties
        {
            get => privateProperties; set { privateProperties = value; OnPropertyChanged(nameof(PrivateProperties)); }
        }

        public bool PublicProperties
        {
            get => publicProperties; set { publicProperties = value; OnPropertyChanged(nameof(PublicProperties)); }
        }

        public bool PublicMethods
        {
            get => publicMethods; set { publicMethods = value; OnPropertyChanged(nameof(PublicMethods)); }
        }

        public bool PrivateMethods
        {
            get => privateMethods; set { privateMethods = value; OnPropertyChanged(nameof(PrivateMethods)); }
        }

        public bool PrivateFields
        {
            get => privateFields; set { privateFields = value; OnPropertyChanged(nameof(PrivateFields)); }
        }

        string errorMessage;

        public string ErrorMessage
        {
            get { return errorMessage; }

            set
            {
                errorMessage = value;
                OnPropertyChanged(nameof(ErrorMessage));
            }
        }

        public void Clear()
        {
            errorMessage = null;
            MemberList.Clear();
        }

        public bool CanParse(string file)
        {
            var fileType = Path.GetExtension(file).ToLower();
            return mappers.ContainsKey(fileType);
        }

        Dictionary<string, Func<string, IEnumerable<MemberInfo>>> mappers = new Dictionary<string, Func<string, IEnumerable<MemberInfo>>>()
        {
            { ".cs", CSharpMapper.Generate },
            { ".py", PythonMapper.Generate },
            { ".pyw", PythonMapper.Generate },
        };

        public void GenerateMap(string file)
        {
            try
            {
                MemberList.Clear();

                var fileType = Path.GetExtension(file).ToLower();
                var generateMap = mappers[fileType];

                foreach (var type in generateMap(file))
                {
                    if (ClassName?.Any() == true &&
                        type.Title.IndexOf(ClassName, StringComparison.OrdinalIgnoreCase) == -1)
                        continue;

                    MemberList.Add(type);

                    List<MemberInfo> typeMembers = new List<MemberInfo>();

                    bool matchingMember(string name)
                    {
                        if (MemberName?.Any() == true)
                            return name.IndexOf(MemberName, StringComparison.OrdinalIgnoreCase) != -1;
                        else
                            return true;
                    }

                    if (type.Children?.Any() == true)
                    {
                        if (PublicMethods || PrivateMethods)
                        {
                            var members = type.Children.Where(x => x.MemberType == MemberType.Method || x.MemberType == MemberType.Constructor);

                            if (!PublicMethods && PrivateMethods) members = members.Where(x => !x.IsPublic);
                            if (PublicMethods && !PrivateMethods) members = members.Where(x => x.IsPublic);

                            foreach (var m in members.OrderBy(x => x.MemberType != MemberType.Constructor)
                                                     .ThenBy(x => x.ToString()))
                            {
                                if (matchingMember(m.Content))
                                    typeMembers.Add(m);
                            }
                        }

                        if (PublicProperties || PrivateProperties)
                        {
                            var members = type.Children.Where(x => x.MemberType == MemberType.Property);

                            if (!PublicProperties && PrivateProperties) members = members.Where(x => !x.IsPublic);
                            if (PublicProperties && !PrivateProperties) members = members.Where(x => x.IsPublic);

                            foreach (var m in members.OrderBy(x => x.ToString()))
                                if (matchingMember(m.Content))
                                    typeMembers.Add(m);
                        }

                        if (PublicFields || PrivateFields)
                        {
                            var members = type.Children.Where(x => x.MemberType == MemberType.Field);

                            if (!PublicFields && PrivateFields) members = members.Where(x => !x.IsPublic);
                            if (PublicFields && !PrivateFields) members = members.Where(x => x.IsPublic);

                            foreach (var m in members.OrderBy(x => x.ToString()))
                                if (matchingMember(m.Content))
                                    typeMembers.Add(m);
                        }
                    }

                    if (!SortMembers)
                        typeMembers = typeMembers.OrderBy(x => x.Line).ToList();

                    typeMembers.ForEach(MemberList.Add);
                }

                ErrorMessage = null;
            }
            catch (Exception e)
            {
                ErrorMessage = e.Message;
            }
        }
    }
}