﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.DTOs;
using API.DTOs.Filtering;
using API.DTOs.Reader;
using API.Entities;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp.PixelFormats;

namespace API.Data.Repositories;

[Flags]
public enum AppUserIncludes
{
    None = 1,
    Progress = 2,
    Bookmarks = 4,
    ReadingLists = 8,
    Ratings = 16,
    UserPreferences = 32,
    WantToRead = 64,
    ReadingListsWithItems = 128,

}

public interface IUserRepository
{
    void Update(AppUser user);
    void Update(AppUserPreferences preferences);
    void Update(AppUserBookmark bookmark);
    public void Delete(AppUser user);
    void Delete(AppUserBookmark bookmark);
    Task<IEnumerable<MemberDto>>  GetEmailConfirmedMemberDtosAsync();
    Task<IEnumerable<MemberDto>> GetPendingMemberDtosAsync();
    Task<IEnumerable<AppUser>> GetAdminUsersAsync();
    Task<bool> IsUserAdminAsync(AppUser user);
    Task<AppUserRating> GetUserRatingAsync(int seriesId, int userId);
    Task<AppUserPreferences> GetPreferencesAsync(string username);
    Task<IEnumerable<BookmarkDto>> GetBookmarkDtosForSeries(int userId, int seriesId);
    Task<IEnumerable<BookmarkDto>> GetBookmarkDtosForVolume(int userId, int volumeId);
    Task<IEnumerable<BookmarkDto>> GetBookmarkDtosForChapter(int userId, int chapterId);
    Task<IEnumerable<BookmarkDto>> GetAllBookmarkDtos(int userId, FilterDto filter);
    Task<IEnumerable<AppUserBookmark>> GetAllBookmarksAsync();
    Task<AppUserBookmark> GetBookmarkForPage(int page, int chapterId, int userId);
    Task<AppUserBookmark> GetBookmarkAsync(int bookmarkId);
    Task<int> GetUserIdByApiKeyAsync(string apiKey);
    Task<AppUser> GetUserByUsernameAsync(string username, AppUserIncludes includeFlags = AppUserIncludes.None);
    Task<AppUser> GetUserByIdAsync(int userId, AppUserIncludes includeFlags = AppUserIncludes.None);
    Task<int> GetUserIdByUsernameAsync(string username);
    Task<IList<AppUserBookmark>> GetAllBookmarksByIds(IList<int> bookmarkIds);
    Task<AppUser> GetUserByEmailAsync(string email);
    Task<IEnumerable<AppUser>> GetAllUsers();
    Task<IEnumerable<AppUserPreferences>> GetAllPreferencesByThemeAsync(int themeId);
    Task<bool> HasAccessToLibrary(int libraryId, int userId);
    Task<IEnumerable<AppUser>> GetAllUsersAsync(AppUserIncludes includeFlags);
}

public class UserRepository : IUserRepository
{
    private readonly DataContext _context;
    private readonly UserManager<AppUser> _userManager;
    private readonly IMapper _mapper;

    public UserRepository(DataContext context, UserManager<AppUser> userManager, IMapper mapper)
    {
        _context = context;
        _userManager = userManager;
        _mapper = mapper;
    }

    public void Update(AppUser user)
    {
        _context.Entry(user).State = EntityState.Modified;
    }

    public void Update(AppUserPreferences preferences)
    {
        _context.Entry(preferences).State = EntityState.Modified;
    }

    public void Update(AppUserBookmark bookmark)
    {
        _context.Entry(bookmark).State = EntityState.Modified;
    }

    public void Delete(AppUser user)
    {
        _context.AppUser.Remove(user);
    }

    public void Delete(AppUserBookmark bookmark)
    {
        _context.AppUserBookmark.Remove(bookmark);
    }

    /// <summary>
    /// A one stop shop to get a tracked AppUser instance with any number of JOINs generated by passing bitwise flags.
    /// </summary>
    /// <param name="username"></param>
    /// <param name="includeFlags">Includes() you want. Pass multiple with flag1 | flag2 </param>
    /// <returns></returns>
    public async Task<AppUser> GetUserByUsernameAsync(string username, AppUserIncludes includeFlags = AppUserIncludes.None)
    {
        var query = _context.Users
            .Where(x => x.UserName == username);

        query = AddIncludesToQuery(query, includeFlags);

        return await query.SingleOrDefaultAsync();
    }

    /// <summary>
    /// A one stop shop to get a tracked AppUser instance with any number of JOINs generated by passing bitwise flags.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="includeFlags">Includes() you want. Pass multiple with flag1 | flag2 </param>
    /// <returns></returns>
    public async Task<AppUser> GetUserByIdAsync(int userId, AppUserIncludes includeFlags = AppUserIncludes.None)
    {
        var query = _context.Users
            .Where(x => x.Id == userId);

        query = AddIncludesToQuery(query, includeFlags);

        return await query.SingleOrDefaultAsync();
    }

    public async Task<IEnumerable<AppUserBookmark>> GetAllBookmarksAsync()
    {
        return await _context.AppUserBookmark.ToListAsync();
    }

    public async Task<AppUserBookmark> GetBookmarkForPage(int page, int chapterId, int userId)
    {
        return await _context.AppUserBookmark
            .Where(b => b.Page == page && b.ChapterId == chapterId && b.AppUserId == userId)
            .SingleOrDefaultAsync();
    }

    public async Task<AppUserBookmark> GetBookmarkAsync(int bookmarkId)
    {
        return await _context.AppUserBookmark
            .Where(b => b.Id == bookmarkId)
            .SingleOrDefaultAsync();
    }

    private static IQueryable<AppUser> AddIncludesToQuery(IQueryable<AppUser> query, AppUserIncludes includeFlags)
    {
        if (includeFlags.HasFlag(AppUserIncludes.Bookmarks))
        {
            query = query.Include(u => u.Bookmarks);
        }

        if (includeFlags.HasFlag(AppUserIncludes.Progress))
        {
            query = query.Include(u => u.Progresses);
        }

        if (includeFlags.HasFlag(AppUserIncludes.ReadingLists))
        {
            query = query.Include(u => u.ReadingLists);
        }

        if (includeFlags.HasFlag(AppUserIncludes.ReadingListsWithItems))
        {
            query = query.Include(u => u.ReadingLists).ThenInclude(r => r.Items);
        }

        if (includeFlags.HasFlag(AppUserIncludes.Ratings))
        {
            query = query.Include(u => u.Ratings);
        }

        if (includeFlags.HasFlag(AppUserIncludes.UserPreferences))
        {
            query = query.Include(u => u.UserPreferences);
        }

        if (includeFlags.HasFlag(AppUserIncludes.WantToRead))
        {
            query = query.Include(u => u.WantToRead);
        }



        return query;
    }


    /// <summary>
    /// This fetches the Id for a user. Use whenever you just need an ID.
    /// </summary>
    /// <param name="username"></param>
    /// <returns></returns>
    public async Task<int> GetUserIdByUsernameAsync(string username)
    {
        return await _context.Users
            .Where(x => x.UserName == username)
            .Select(u => u.Id)
            .SingleOrDefaultAsync();
    }


    /// <summary>
    /// Returns all Bookmarks for a given set of Ids
    /// </summary>
    /// <param name="bookmarkIds"></param>
    /// <returns></returns>
    public async Task<IList<AppUserBookmark>> GetAllBookmarksByIds(IList<int> bookmarkIds)
    {
        return await _context.AppUserBookmark
            .Where(b => bookmarkIds.Contains(b.Id))
            .OrderBy(b => b.Created)
            .ToListAsync();
    }

    public async Task<AppUser> GetUserByEmailAsync(string email)
    {
        var lowerEmail = email.ToLower();
        return await _context.AppUser.SingleOrDefaultAsync(u => u.Email.ToLower().Equals(lowerEmail));
    }

    public async Task<IEnumerable<AppUser>> GetAllUsers()
    {
        return await _context.AppUser.ToListAsync();
    }

    public async Task<IEnumerable<AppUserPreferences>> GetAllPreferencesByThemeAsync(int themeId)
    {
        return await _context.AppUserPreferences
            .Include(p => p.Theme)
            .Where(p => p.Theme.Id == themeId)
            .AsSplitQuery()
            .ToListAsync();
    }

    public async Task<bool> HasAccessToLibrary(int libraryId, int userId)
    {
        return await _context.Library
            .Include(l => l.AppUsers)
            .AsSplitQuery()
            .AnyAsync(library => library.AppUsers.Any(user => user.Id == userId));
    }

    public async Task<IEnumerable<AppUser>> GetAllUsersAsync(AppUserIncludes includeFlags)
    {
        var query = AddIncludesToQuery(_context.Users.AsQueryable(), includeFlags);
        return await query.ToListAsync();
    }

    public async Task<IEnumerable<AppUser>> GetAdminUsersAsync()
    {
        return await _userManager.GetUsersInRoleAsync(PolicyConstants.AdminRole);
    }

    public async Task<bool> IsUserAdminAsync(AppUser user)
    {
        return await _userManager.IsInRoleAsync(user, PolicyConstants.AdminRole);
    }

    public async Task<AppUserRating> GetUserRatingAsync(int seriesId, int userId)
    {
        return await _context.AppUserRating
            .Where(r => r.SeriesId == seriesId && r.AppUserId == userId)
            .SingleOrDefaultAsync();
    }

    public async Task<AppUserPreferences> GetPreferencesAsync(string username)
    {
        return await _context.AppUserPreferences
            .Include(p => p.AppUser)
            .Include(p => p.Theme)
            .AsSplitQuery()
            .SingleOrDefaultAsync(p => p.AppUser.UserName == username);
    }

    public async Task<IEnumerable<BookmarkDto>> GetBookmarkDtosForSeries(int userId, int seriesId)
    {
        return await _context.AppUserBookmark
            .Where(x => x.AppUserId == userId && x.SeriesId == seriesId)
            .OrderBy(x => x.Page)
            .AsNoTracking()
            .ProjectTo<BookmarkDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<IEnumerable<BookmarkDto>> GetBookmarkDtosForVolume(int userId, int volumeId)
    {
        return await _context.AppUserBookmark
            .Where(x => x.AppUserId == userId && x.VolumeId == volumeId)
            .OrderBy(x => x.Page)
            .AsNoTracking()
            .ProjectTo<BookmarkDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<IEnumerable<BookmarkDto>> GetBookmarkDtosForChapter(int userId, int chapterId)
    {
        return await _context.AppUserBookmark
            .Where(x => x.AppUserId == userId && x.ChapterId == chapterId)
            .OrderBy(x => x.Page)
            .AsNoTracking()
            .ProjectTo<BookmarkDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    /// <summary>
    /// Get all bookmarks for the user
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="filter">Only supports SeriesNameQuery</param>
    /// <returns></returns>
    public async Task<IEnumerable<BookmarkDto>> GetAllBookmarkDtos(int userId, FilterDto filter)
    {
        var query = _context.AppUserBookmark
            .Where(x => x.AppUserId == userId)
            .OrderBy(x => x.Page)
            .AsNoTracking();

        if (!string.IsNullOrEmpty(filter.SeriesNameQuery))
        {
            var seriesNameQueryNormalized = Services.Tasks.Scanner.Parser.Parser.Normalize(filter.SeriesNameQuery);
            var filterSeriesQuery = query.Join(_context.Series, b => b.SeriesId, s => s.Id, (bookmark, series) => new
                {
                    bookmark,
                    series
                })
                .Where(o => EF.Functions.Like(o.series.Name, $"%{filter.SeriesNameQuery}%")
                            || EF.Functions.Like(o.series.OriginalName, $"%{filter.SeriesNameQuery}%")
                            || EF.Functions.Like(o.series.LocalizedName, $"%{filter.SeriesNameQuery}%")
                            || EF.Functions.Like(o.series.NormalizedName, $"%{seriesNameQueryNormalized}%")
                );

            // This doesn't work on bookmarks themselves, only the series. For now, I don't think there is much value add
            // if (filter.SortOptions != null)
            // {
            //     if (filter.SortOptions.IsAscending)
            //     {
            //         filterSeriesQuery = filter.SortOptions.SortField switch
            //         {
            //             SortField.SortName => filterSeriesQuery.OrderBy(s => s.series.SortName),
            //             SortField.CreatedDate => filterSeriesQuery.OrderBy(s => s.bookmark.Created),
            //             SortField.LastModifiedDate => filterSeriesQuery.OrderBy(s => s.bookmark.LastModified),
            //             _ => filterSeriesQuery
            //         };
            //     }
            //     else
            //     {
            //         filterSeriesQuery = filter.SortOptions.SortField switch
            //         {
            //             SortField.SortName => filterSeriesQuery.OrderByDescending(s => s.series.SortName),
            //             SortField.CreatedDate => filterSeriesQuery.OrderByDescending(s => s.bookmark.Created),
            //             SortField.LastModifiedDate => filterSeriesQuery.OrderByDescending(s => s.bookmark.LastModified),
            //             _ => filterSeriesQuery
            //         };
            //     }
            // }

            query = filterSeriesQuery.Select(o => o.bookmark);
        }


        return await query
            .ProjectTo<BookmarkDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    /// <summary>
    /// Fetches the UserId by API Key. This does not include any extra information
    /// </summary>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    public async Task<int> GetUserIdByApiKeyAsync(string apiKey)
    {
        return await _context.AppUser
            .Where(u => u.ApiKey.Equals(apiKey))
            .Select(u => u.Id)
            .SingleOrDefaultAsync();
    }


    public async Task<IEnumerable<MemberDto>> GetEmailConfirmedMemberDtosAsync()
    {
        return await _context.Users
            .Where(u => u.EmailConfirmed)
            .Include(x => x.Libraries)
            .Include(r => r.UserRoles)
            .ThenInclude(r => r.Role)
            .OrderBy(u => u.UserName)
            .Select(u => new MemberDto
            {
                Id = u.Id,
                Username = u.UserName,
                Email = u.Email,
                Created = u.Created,
                LastActive = u.LastActive,
                Roles = u.UserRoles.Select(r => r.Role.Name).ToList(),
                Libraries =  u.Libraries.Select(l => new LibraryDto
                {
                    Name = l.Name,
                    Type = l.Type,
                    LastScanned = l.LastScanned,
                    Folders = l.Folders.Select(x => x.Path).ToList()
                }).ToList()
            })
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<MemberDto>> GetPendingMemberDtosAsync()
    {
        return await _context.Users
            .Where(u => !u.EmailConfirmed)
            .Include(x => x.Libraries)
            .Include(r => r.UserRoles)
            .ThenInclude(r => r.Role)
            .OrderBy(u => u.UserName)
            .Select(u => new MemberDto
            {
                Id = u.Id,
                Username = u.UserName,
                Email = u.Email,
                Created = u.Created,
                LastActive = u.LastActive,
                Roles = u.UserRoles.Select(r => r.Role.Name).ToList(),
                Libraries =  u.Libraries.Select(l => new LibraryDto
                {
                    Name = l.Name,
                    Type = l.Type,
                    LastScanned = l.LastScanned,
                    Folders = l.Folders.Select(x => x.Path).ToList()
                }).ToList()
            })
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync();
    }
}
