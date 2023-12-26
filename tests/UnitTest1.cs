using System.Collections.ObjectModel;

namespace PropertyBind.Test;

public class UnitTest1
{
	[Fact]
	public void Test1()
	{
		var blog = new Blog();

		var post = new Post() { Blog = new Blog() };

		Assert.NotEqual(blog, post.Blog);
		blog.Posts.Add(post);
		Assert.Equal(blog, post.Blog);
	}
}

[GeneratePropertyBind(nameof(Posts), nameof(Post.Blog))]
public partial class Blog
{
	public ObservableCollection<Post> Posts { get; } = new();
}

public class Post
{
	public Blog Blog { get; set; } = null!;
}

//partial class Blog
//{
//	public Blog()
//	{
//		Posts.CollectionChanged += Posts_CollectionChanged;
//	}

//	private void Posts_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
//	{
//		if (e.Action == NotifyCollectionChangedAction.Add)
//		{
//			if (e.NewItems == null) return;
//			foreach (Post item in e.NewItems)
//			{
//				item.Blog = this;
//			}
//		}
//	}
//}


