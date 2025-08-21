using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using VideoWebPlayer.Client;
using VideoWebPlayer.Components;
using VideoWebPlayer.Components.Account;
using VideoWebPlayer.Data;
using VideoWebPlayer.Extensions;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.AddVideoWebPlayerServices();

var app = builder.Build();
app.MigrateDatabase();
app.UseVideoWebPlayer();
app.Run();
