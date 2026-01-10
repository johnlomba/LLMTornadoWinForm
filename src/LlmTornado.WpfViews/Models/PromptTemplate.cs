using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LlmTornado.WpfViews.Models;

/// <summary>
/// Represents a system prompt template.
/// </summary>
public class PromptTemplate : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString();
    private string _name = string.Empty;
    private string _content = string.Empty;
    private string? _description;
    private bool _isBuiltIn;
    private DateTime _createdAt = DateTime.UtcNow;
    
    /// <summary>
    /// Unique identifier for the template.
    /// </summary>
    public string Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }
    
    /// <summary>
    /// Display name for the template.
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }
    
    /// <summary>
    /// The actual system prompt content.
    /// </summary>
    public string Content
    {
        get => _content;
        set => SetField(ref _content, value);
    }
    
    /// <summary>
    /// Optional description of what this template is for.
    /// </summary>
    public string? Description
    {
        get => _description;
        set => SetField(ref _description, value);
    }
    
    /// <summary>
    /// Whether this is a built-in template or user-created.
    /// </summary>
    public bool IsBuiltIn
    {
        get => _isBuiltIn;
        set => SetField(ref _isBuiltIn, value);
    }
    
    /// <summary>
    /// When the template was created.
    /// </summary>
    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetField(ref _createdAt, value);
    }
    
    /// <summary>
    /// Event raised when a property changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Creates default built-in templates.
    /// </summary>
    public static List<PromptTemplate> GetBuiltInTemplates()
    {
        return
        [
            new PromptTemplate
            {
                Id = "default",
                Name = "Default",
                Content = "You are a helpful assistant.",
                Description = "A general-purpose helpful assistant.",
                IsBuiltIn = true
            },
            new PromptTemplate
            {
                Id = "school-teacher",
                Name = "School Teacher",
                Content = "You are an elementary school teacher. Please try to explain things in a way that is easy for children to understand.",
                Description = "Explains concepts in simple, child-friendly terms.",
                IsBuiltIn = true
            },
            new PromptTemplate
            {
                Id = "code-expert",
                Name = "Code Expert",
                Content = "You are an expert software developer. Provide clear, well-documented code examples and explain technical concepts thoroughly. Always consider best practices, performance, and maintainability.",
                Description = "Expert programmer providing code assistance.",
                IsBuiltIn = true
            },
            new PromptTemplate
            {
                Id = "creative-writer",
                Name = "Creative Writer",
                Content = "You are a creative writer with expertise in storytelling, poetry, and prose. Help users with creative writing tasks, provide inspiration, and offer constructive feedback on their work.",
                Description = "Assists with creative writing and storytelling.",
                IsBuiltIn = true
            },
            new PromptTemplate
            {
                Id = "data-analyst",
                Name = "Data Analyst",
                Content = "You are a data analyst expert. Help users understand data, create analyses, explain statistical concepts, and provide insights from data. Be precise with numbers and always explain your methodology.",
                Description = "Expert data analysis and statistics assistance.",
                IsBuiltIn = true
            },
            new PromptTemplate
            {
                Id = "translator",
                Name = "Translator",
                Content = "You are a professional translator fluent in multiple languages. Provide accurate translations while preserving the meaning, tone, and cultural nuances of the original text.",
                Description = "Professional translation assistance.",
                IsBuiltIn = true
            }
        ];
    }
}
