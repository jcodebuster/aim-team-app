﻿using System;
using System.Collections.Generic;
using System.Globalization;
using BLL.Abstractions.Interfaces;
using Core;
using DAL.Abstractions.Interfaces;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using BLL.Helpers;

namespace BLL.Services
{
    public class UrlInvitationService : IUrlInvitationService
    {
        private string _domen = "aim-app.net";
        private readonly IGenericRepository<Urls> _genericRepositoryUrls;
        private readonly IGenericRepository<Room> _genericRepositoryRooms;
        private readonly ICurrentUser _currentUser;
        private readonly IMailWorker _mailWorker;
        private readonly IUserService _userService;

        public UrlInvitationService(IGenericRepository<Urls> genericRepositoryUrls,
            IGenericRepository<Room> genericRepositoryRooms, ICurrentUser currentUser,
            IMailWorker mailWorker, IUserService userService)
        {
            this._genericRepositoryUrls = genericRepositoryUrls;
            this._genericRepositoryRooms = genericRepositoryRooms;
            this._currentUser = currentUser;
            this._mailWorker = mailWorker;
            this._userService = userService;
        }

        public void InviteUsersByEmailWithUrl(Room roomId, string[] users)
        {
            var currentTime = DateTime.Now;
            var expirationTime =
                currentTime.AddMinutes(5).ToString("yyyy.mm.dd.hh.mm.ss", CultureInfo.InvariantCulture);
            var result = new List<string>();
            var url = new Urls();
            url.RoomId = roomId.Id;
            var body = $"Invitation to join{roomId.RoomName} from{_currentUser.User.UserName}";
            if (users.Length == 1)
            {
                var userId = _userService.GetUserByUserNameOrEmail(users[0]).Result;
                result.Add(userId.Id);
                url = new Urls
                {
                    Url = _domen + Guid.NewGuid().ToString().Substring(0, 7),
                    UserId = result,
                    ExpirationTime = expirationTime
                };

                var mailTo = new MailAddress(userId.Email);
                _mailWorker.SendMailMessageAsync(mailTo, "Invitation to the room", body);
            }

            if (users.Length > 1)
            {
                foreach (var user in users)
                {
                    var userId = _userService.GetUserByUserNameOrEmail(user).Result;
                    result.Add(userId.Id);
                    var mailTo = new MailAddress(userId.Email);
                    _mailWorker.SendMailMessageAsync(mailTo, "Invitation to the room", body);
                }

                url = new Urls
                {
                    Url = _domen + "roomid=" + roomId + Guid.NewGuid().ToString().Substring(0, 7),
                    UserId = result,
                    ExpirationTime = expirationTime
                };
            }

            _genericRepositoryUrls.CreateAsync(url);
        }

        public string InviteUsersByUrl(Room room)
        {
            var currentTime = DateTime.Now;
            var expirationTime =
                currentTime.AddHours(5).ToString("yyyy.mm.dd.hh.mm.ss", CultureInfo.InvariantCulture);
            var url = new Urls
            {
                Url = _domen + Guid.NewGuid().ToString().Substring(0, 6),
                ExpirationTime = expirationTime,
                UserId = null,
                RoomId = room.Id
            };

            _genericRepositoryUrls.CreateAsync(url);
            return url.Url;
        }

        public async Task<bool> JoinByUrl(string url)
        {
            var urlFromDb = await _genericRepositoryUrls.FindByConditionAsync(urls => urls.Url == url);

            var urlsEnumerable = urlFromDb.ToList();
            if (urlsEnumerable.Any())
            {
                var expectedUserId = urlsEnumerable.First();
                if (expectedUserId.UserId.Contains(_currentUser.User.Id))
                {
                    //ToDO: put user into room
                    var rooms = await _genericRepositoryRooms.FindByConditionAsync(room =>
                        room.Id == expectedUserId.RoomId);
                    var room = rooms.FirstOrDefault();
                    var participantInfo = new ParticipantInfo()
                    {
                        Notifications = true, User = _currentUser.User, RoleId = "0"
                    };

                    room.Participants.Add(participantInfo);

                    await _genericRepositoryRooms.UpdateAsync(room);
                    return true;
                }
            }

            return false;
        }
    }
}
