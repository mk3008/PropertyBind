# PropertyBind
![GitHub](https://img.shields.io/github/license/mk3008/PropertyBind)
![GitHub code size in bytes](https://img.shields.io/github/languages/code-size/mk3008/PropertyBind)
![Github Last commit](https://img.shields.io/github/last-commit/mk3008/PropertyBind)  
[![SqModel](https://img.shields.io/nuget/v/PropertyBind.svg)](https://www.nuget.org/packages/PropertyBind/) 
[![SqModel](https://img.shields.io/nuget/dt/PropertyBind.svg)](https://www.nuget.org/packages/PropertyBind/) 

Property synchronization process using Source Generator.

## Demo
Suppose that the Blog class has a Post class collection property, and the Post class has a Blog property.

When handling a common Blog instance between two classes, it is necessary to detect when an item is added to the collection property and set itself to the Blog property.

Specifically, you need to write the following code.

```cs
using System.Collections.ObjectModel;
using System.Collections.Specialized;

public class Blog
{
	public ObservableCollection<Post> Posts { get; } = new();

	public Blog()
	{
		Posts.CollectionChanged += Posts_CollectionChanged;
	}

	private void Posts_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.Action == NotifyCollectionChangedAction.Add)
		{
			if (e.NewItems == null) return;
			foreach (Post item in e.NewItems)
			{
				item.Blog = this;
			}
		}
	}
}

public class Post
{
	public Blog Blog { get; set; } = null!;
}
```

The above code is boring.

By using SourceGenerator PropertyBind, you can write a shorter code with the same meaning as above.

```cs
[GeneratePropertyBind(nameof(Posts), nameof(Post.Blog))]
public partial class Blog
{
	public IList<Post> Posts { get; }
}

public class Post
{
	public Blog Blog { get; set; } = null!;
}
```

Specify the property name of the collection in the first argument.

In the second argument, specify the property name you want to associate with itself.

When executed, the following behavior occurs.
```cs
[Fact]
public void Test1()
{
	var blog = new Blog();
	var post = new Post() { Blog = new Blog() };

	Assert.NotEqual(blog, post.Blog);
	blog.Posts.Add(post);
	Assert.Equal(blog, post.Blog);
}
```

## Note: auto-generated code

### GeneratePropertyBindAttribute.cs
```cs
namespace PropertyBind
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    internal sealed class GeneratePropertyBindAttribute : Attribute
    {
        public string ObservableCollectionPropertyName { get; } 
        public string BindPropertyName { get; } 
        public GeneratePropertyBindAttribute(string observableCollectionPropertyName, string bindPropertyName)
        {
            this.ObservableCollectionPropertyName = observableCollectionPropertyName;
			this.BindPropertyName = bindPropertyName;
        }
    }
}
```

### Blog.g.cs
```cs
using System.Collections.Specialized;

public partial class Blog
{
	public Blog()
	{
		var lst = new ObservableCollection<Post>();
		lst.CollectionChanged += __Posts_CollectionChanged;
		Posts = lst
	}

	private void __Posts_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.Action == NotifyCollectionChangedAction.Add)
		{
			if (e.NewItems == null) return;
			foreach (Post item in e.NewItems)
			{
				item.Blog = this;
			}
		}
	}
}
```
