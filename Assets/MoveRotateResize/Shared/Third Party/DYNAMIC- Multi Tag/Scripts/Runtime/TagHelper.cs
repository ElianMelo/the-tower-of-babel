//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.2
//------------------------------------------------------------------------------
using UnityEngine;

namespace MoveRotateResize.Dynamic.MultiTag
{
	public static class TagHelper  
	{
		public static void AddTag(this GameObject instance, Tag tag)
		{
			//Debug.Log($"[AddTag] Adding tag: {tag.name} to GameObject: {instance.name}");
#if UNITY_EDITOR
			var taggable = GetComponent(instance);
			if (!taggable.Contains(tag))
			{
				//Debug.Log($"[AddTag] Tag {tag.name} not found in {instance.name}, adding in editor.");
				taggable.Add(tag);
			}
#endif
			tag.Add(instance, instance.GetHashCode());
		}

		public static void AddTag(this Component instance, Tag tag)
		{
			Debug.Log($"[AddTag] Adding tag: {tag.name} to Component: {instance.GetType().Name}");
			instance.gameObject.AddTag(tag);
		}

		public static void RemoveTag(this GameObject instance, Tag tag)
		{
			//Debug.Log($"[RemoveTag] Removing tag: {tag.name} from GameObject: {instance.name}");
#if UNITY_EDITOR
			var taggable = GetComponent(instance);
			if (taggable.Contains(tag))
			{
				//Debug.Log($"[RemoveTag] Tag {tag.name} found in {instance.name}, removing in editor.");
				taggable.Remove(tag);
			}
#endif
			tag.Remove(instance, instance.GetHashCode());
		}

		public static void RemoveTag(this Component instance, Tag tag)
		{
			//Debug.Log($"[RemoveTag] Removing tag: {tag.name} from Component: {instance.GetType().Name}");
			instance.gameObject.RemoveTag(tag);
		}

		public static bool HasTag(this GameObject instance, Tag tag)
		{
			bool hasTag = tag.HasInstance(instance.GetHashCode());
			//Debug.Log($"[HasTag] GameObject: {instance.name} has tag {tag.name}: {hasTag}");
			return hasTag;
		}

		public static bool HasTag(this Component instance, Tag tag)
		{
			//Debug.Log($"[HasTag] Checking if Component: {instance.GetType().Name} has tag: {tag.name}");
			return instance.gameObject.HasTag(tag);
		}

		public static void AddTags(this GameObject instance, Tag[] tags)
		{
			//Debug.Log($"[AddTags] Adding tags to GameObject: {instance.name}");
#if UNITY_EDITOR
			var taggable = GetComponent(instance);
#endif
			int hash = instance.GetHashCode();

			foreach (var tag in tags)
			{
				//Debug.Log($"[AddTags] Adding tag: {tag.name} to GameObject: {instance.name}");
#if UNITY_EDITOR
				if (!taggable.Contains(tag))
				{
					//Debug.Log($"[AddTags] Tag {tag.name} not found in {instance.name}, adding in editor.");
					taggable.Add(tag);
				}
#endif
				tag.Add(instance, hash);
			}
		}

		public static void AddTags(this Component instance, Tag[] tags)
		{
			//Debug.Log($"[AddTags] Adding tags to Component: {instance.GetType().Name}");
			instance.gameObject.AddTags(tags);
		}

		public static void AddTags(this GameObject instance, TagArray composite)
		{
			//Debug.Log($"[AddTags] Adding composite tags to GameObject: {instance.name}");
#if UNITY_EDITOR
			var taggable = GetComponent(instance);
			foreach (var tag in composite.Tags)
			{
				if (!taggable.Contains(tag))
				{
					//Debug.Log($"[AddTags] Composite tag {tag.name} not found in {instance.name}, adding in editor.");
					taggable.Add(tag);
				}
			}
#endif
			composite.Add(instance, instance.GetHashCode());
		}

		public static void AddTags(this Component instance, TagArray composite)
		{
			//Debug.Log($"[AddTags] Adding composite tags to Component: {instance.GetType().Name}");
			instance.gameObject.AddTags(composite);
		}

		public static void RemoveTags(this GameObject instance, Tag[] tags)
		{
			//Debug.Log($"[RemoveTags] Removing tags from GameObject: {instance.name}");
#if UNITY_EDITOR
			var taggable = GetComponent(instance);
#endif
			int hash = instance.GetHashCode();

			foreach (var tag in tags)
			{
				//Debug.Log($"[RemoveTags] Removing tag: {tag.name} from GameObject: {instance.name}");
#if UNITY_EDITOR
				if (taggable.Contains(tag))
				{
					//Debug.Log($"[RemoveTags] Tag {tag.name} found in {instance.name}, removing in editor.");
					taggable.Remove(tag);
				}
#endif
				tag.Remove(instance, hash);
			}
		}

		public static void RemoveTags(this Component instance, Tag[] tags)
		{
			//Debug.Log($"[RemoveTags] Removing tags from Component: {instance.GetType().Name}");
			instance.gameObject.RemoveTags(tags);
		}

		public static void RemoveTags(this GameObject instance, TagArray composite)
		{
			//Debug.Log($"[RemoveTags] Removing composite tags from GameObject: {instance.name}");
#if UNITY_EDITOR
			var taggable = GetComponent(instance);
			foreach (var tag in composite.Tags)
			{
				if (taggable.Contains(tag))
				{
					//Debug.Log($"[RemoveTags] Composite tag {tag.name} found in {instance.name}, removing in editor.");
					taggable.Remove(tag);
				}
			}
#endif
			composite.Remove(instance, instance.GetHashCode());
		}

		public static void RemoveTags(this Component instance, TagArray composite)
		{
			//Debug.Log($"[RemoveTags] Removing composite tags from Component: {instance.GetType().Name}");
			instance.gameObject.RemoveTags(composite);
		}

		public static bool HasTags(this GameObject instance, Tag[] tags, bool allRequired)
		{
			//Debug.Log($"[HasTags] Checking tags on GameObject: {instance.name}, allRequired: {allRequired}");
			int hash = instance.GetHashCode();

			foreach (var tag in tags)
			{
				bool hasTag = tag.HasInstance(hash);
				//Debug.Log($"[HasTags] Tag {tag.name} on GameObject: {instance.name}: {hasTag}");
				if (hasTag == !allRequired)
					return !allRequired;
			}

			return allRequired;
		}

		public static bool HasTags(this Component instance, Tag[] tags, bool allRequired)
		{
			//Debug.Log($"[HasTags] Checking tags on Component: {instance.GetType().Name}, allRequired: {allRequired}");
			return instance.gameObject.HasTags(tags, allRequired);
		}

		public static bool HasTags(this GameObject instance, TagArray composite, bool allRequired)
		{
			//Debug.Log($"[HasTags] Checking composite tags on GameObject: {instance.name}, allRequired: {allRequired}");
			return composite.HasInstance(instance, allRequired);
		}

		public static bool HasTags(this Component instance, TagArray composite, bool allRequired)
		{
			//Debug.Log($"[HasTags] Checking composite tags on Component: {instance.GetType().Name}, allRequired: {allRequired}");
			return instance.gameObject.HasTags(composite, allRequired);
		}

#if UNITY_EDITOR
		private static DynamicMultiTag GetComponent(GameObject instance)
		{
			if (!instance.TryGetComponent(out DynamicMultiTag taggable))
			{
				//Debug.Log($"[GetComponent] DynamicMultiTag not found on {instance.name}, adding new component.");
				taggable = instance.AddComponent<DynamicMultiTag>();
			}
			return taggable;
		}
#endif
	}
}