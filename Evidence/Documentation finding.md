# CacheView documentation finding

Checked: 2026-09-16

Source: [ASP.NET Core Blazor CacheView component](https://learn.microsoft.com/aspnet/core/blazor/state-management/cacheview-component?view=aspnetcore-11.0)

Article date: 2026-09-15

## Finding

Yes. The documentation explicitly identifies the risk of caching user-specific content without the corresponding vary-by dimension.

Under **Cache keys and vary-by values**, it states: "Without a matching vary-by parameter, requests with different values share the same cached output." The parameter table identifies `VaryByUser` as varying the cache by authenticated user identity.

Under **Limitations > Live component parameters are captured once**, the documentation labels a `CacheView` that receives the current user's name without `VaryByUser` as "unsafe." It explains that if Alice creates the entry, Bob can receive the captured value "Alice." The documented mitigations are to set `VaryByUser="true"` or move the user-specific component outside `CacheView`.

The documentation also describes opt-in enforcement for component authors through `CacheBehavior.Throw` and `CacheCondition(CacheVaryBy.User)`. `AuthorizeView` has this protection built in and throws unless the enclosing cache varies by user. Arbitrary custom components aren't automatically protected unless they declare a cache behavior/condition.

## Conclusion

The documented behavior matches this validation's no-variation scenario: omitting `VaryByUser` permits authenticated users to share one cache entry and can disclose one user's rendered content to another user. The framework provides safeguards for participating components, but the application remains responsible for selecting every vary-by dimension used by custom cached content.
