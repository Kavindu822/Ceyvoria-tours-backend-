using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using CeyvoriaTours.Application;
using CeyvoriaTours.Domain;
using CeyvoriaTours.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder=WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") 
    ?? Environment.GetEnvironmentVariable("WEBSITES_PORT") 
    ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var connection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["DefaultConnection"]
    ?? builder.Configuration["DATABASE_URL"]
    ?? builder.Configuration["ConnectionStrings.DefaultConnection"]
    ?? builder.Configuration["ConnectionStrings_DefaultConnection"];

if(string.IsNullOrWhiteSpace(connection))throw new InvalidOperationException("Configure database connection string via DefaultConnection or ConnectionStrings__DefaultConnection.");

var secret = builder.Configuration["Jwt:Secret"]
    ?? builder.Configuration["Jwt_Secret"]
    ?? builder.Configuration["JWT_SECRET"]
    ?? "";

if(Encoding.UTF8.GetByteCount(secret)<48)throw new InvalidOperationException("Jwt secret must be at least 48 bytes. Set Jwt:Secret or Jwt_Secret.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? builder.Configuration["Jwt_Issuer"] ?? "CeyvoriaTours";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? builder.Configuration["Jwt_Audience"] ?? "CeyvoriaToursWebsite";

builder.Services.AddDbContext<CeyvoriaDb>(o=>o.UseNpgsql(connection));
builder.Services.AddScoped<IPasswordHasher<AppUser>,PasswordHasher<AppUser>>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

var rawOrigins=builder.Configuration["CORS_ALLOWED_ORIGINS"]??builder.Configuration["Cors:AllowedOrigins"];
var allowedOrigins=rawOrigins?.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)
 ??builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
 ??["http://localhost:3000"];
builder.Services.AddCors(o=>o.AddPolicy("frontend",p=>p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o=>{
 o.TokenValidationParameters=new TokenValidationParameters{
  ValidateIssuer=true,ValidIssuer=jwtIssuer,
  ValidateAudience=true,ValidAudience=jwtAudience,
  ValidateIssuerSigningKey=true,IssuerSigningKey=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
  ValidateLifetime=true,ClockSkew=TimeSpan.FromMinutes(1)
 };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o=>{
 o.RejectionStatusCode=429;
 o.AddPolicy("forms",context=>RateLimitPartition.GetFixedWindowLimiter(
  context.Connection.RemoteIpAddress?.ToString()??"unknown",
  _=>new FixedWindowRateLimiterOptions{PermitLimit=6,Window=TimeSpan.FromMinutes(5),QueueLimit=0,AutoReplenishment=true}));
 o.AddPolicy("auth",context=>RateLimitPartition.GetFixedWindowLimiter(
  context.Connection.RemoteIpAddress?.ToString()??"unknown",
  _=>new FixedWindowRateLimiterOptions{PermitLimit=10,Window=TimeSpan.FromMinutes(10),QueueLimit=0,AutoReplenishment=true}));
});

var app=builder.Build();
app.UseExceptionHandler();
if(app.Environment.IsDevelopment()){app.UseSwagger();app.UseSwaggerUI();}
app.UseHttpsRedirection();
app.UseCors("frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health",()=>Results.Ok(new{status="ok",service="ceyvoria-api"}));
app.MapGet("/api/tours",async(CeyvoriaDb db,CancellationToken ct)=>{
 var list=await db.Tours.AsNoTracking().Where(x=>x.Published).OrderBy(x=>x.Title).ToListAsync(ct);
 return Ok(list.Select(ToTour).ToList());
});
app.MapGet("/api/tours/{slug}",async(string slug,CeyvoriaDb db,CancellationToken ct)=>{
 var x=await db.Tours.AsNoTracking().FirstOrDefaultAsync(t=>t.Slug==slug&&t.Published,ct);
 return x is null?Results.NotFound():Ok(ToTour(x));
});
app.MapGet("/api/destinations",async(CeyvoriaDb db,CancellationToken ct)=>{
 var list=await db.Destinations.AsNoTracking().Where(x=>x.Published).OrderBy(x=>x.Name).ToListAsync(ct);
 return Ok(list.Select(ToDestination).ToList());
});
app.MapGet("/api/destinations/{slug}",async(string slug,CeyvoriaDb db,CancellationToken ct)=>{
 var x=await db.Destinations.AsNoTracking().FirstOrDefaultAsync(t=>t.Slug==slug&&t.Published,ct);
 return x is null?Results.NotFound():Ok(ToDestination(x));
});
app.MapGet("/api/experiences",async(CeyvoriaDb db,CancellationToken ct)=>Ok(await db.Experiences.AsNoTracking().Where(x=>x.Published).Select(x=>new{x.Name,x.Slug,x.Description,x.Category,x.Image}).ToListAsync(ct)));
app.MapGet("/api/experiences/{slug}",async(string slug,CeyvoriaDb db,CancellationToken ct)=>{
 var x=await db.Experiences.AsNoTracking().FirstOrDefaultAsync(e=>e.Slug==slug&&e.Published,ct);
 return x is null?Results.NotFound():Ok(new{x.Name,x.Slug,x.Description,x.Category,x.Image});
});
app.MapGet("/api/travel-styles",async(CeyvoriaDb db,CancellationToken ct)=>Ok(await db.TravelStyles.AsNoTracking().Where(x=>x.Published).Select(x=>new{x.Name,x.Slug,x.Description,x.Image}).ToListAsync(ct)));
app.MapGet("/api/blog",async(CeyvoriaDb db,CancellationToken ct)=>Ok(await db.BlogPosts.AsNoTracking().Where(x=>x.Published).OrderByDescending(x=>x.CreatedAt).Select(x=>new{x.Title,x.Slug,x.Excerpt,x.Category,x.Image,x.Content}).ToListAsync(ct)));
app.MapGet("/api/blog/{slug}",async(string slug,CeyvoriaDb db,CancellationToken ct)=>{
 var x=await db.BlogPosts.AsNoTracking().FirstOrDefaultAsync(b=>b.Slug==slug&&b.Published,ct);
 return x is null?Results.NotFound():Ok(new{x.Title,x.Slug,x.Excerpt,x.Category,x.Image,x.Content});
});
app.MapGet("/api/faqs",async(CeyvoriaDb db,CancellationToken ct)=>Ok(await db.Faqs.AsNoTracking().Where(x=>x.Published).Select(x=>new{x.Question,x.Answer,x.Category}).ToListAsync(ct)));

app.MapPost("/api/contact",async(ContactRequest input,CeyvoriaDb db,CancellationToken ct)=>{
 var bad=Validate(input);if(bad is not null)return bad;
 var msg=new ContactMessage{Reference=Reference("MSG"),Name=input.Name.Trim(),Email=input.Email.Trim().ToLowerInvariant(),Phone=input.Phone,Country=input.Country,Subject=input.Subject.Trim(),Message=input.Message.Trim()};
 db.ContactMessages.Add(msg);await db.SaveChangesAsync(ct);
 return Results.Ok(new ApiResponse<ReferenceResponse>(true,new(msg.Reference)));
}).RequireRateLimiting("forms");
app.MapPost("/api/custom-trips",async(TripRequest input,CeyvoriaDb db,CancellationToken ct)=>{
 var bad=Validate(input);if(bad is not null)return bad;
 if(input.TravelDate is null&&!input.FlexibleDates)return Results.BadRequest(new{success=false,message="Please supply a date or mark dates flexible."});
 var trip=new CustomTripRequest{Reference=Reference("TRIP"),FullName=input.FullName.Trim(),Email=input.Email.Trim().ToLowerInvariant(),Phone=input.Phone,Country=input.Country,TravelDate=input.TravelDate,FlexibleDates=input.FlexibleDates,Travellers=input.Travellers,DurationDays=input.DurationDays,TravelStyle=input.TravelStyle,InterestsJson=JsonSerializer.Serialize(input.Interests),Accommodation=input.Accommodation,Budget=input.Budget,Currency=input.Currency,SpecialRequirements=input.SpecialRequirements};
 db.CustomTrips.Add(trip);await db.SaveChangesAsync(ct);
 return Results.Ok(new ApiResponse<ReferenceResponse>(true,new(trip.Reference)));
}).RequireRateLimiting("forms");
app.MapPost("/api/bookings",async(BookingRequest input,CeyvoriaDb db,CancellationToken ct)=>{
 var bad=Validate(input);if(bad is not null)return bad;
 if(input.StartDate<DateOnly.FromDateTime(DateTime.UtcNow))return Results.BadRequest(new{success=false,message="Choose a future date."});
 var tour=await db.Tours.FirstOrDefaultAsync(t=>t.Slug==input.TourSlug&&t.Published,ct);
 if(tour is null)return Results.BadRequest(new{success=false,message="Please choose a valid journey."});
 var booking=new Booking{Reference=Reference("BOOK"),TourId=tour.Id,StartDate=input.StartDate,Travellers=input.Travellers,FirstName=input.FirstName.Trim(),LastName=input.LastName.Trim(),Email=input.Email.Trim().ToLowerInvariant(),Phone=input.Phone,SpecialRequests=input.SpecialRequests,Status="PendingEnquiry",PaymentStatus="NotRequested"};
 db.Bookings.Add(booking);await db.SaveChangesAsync(ct);
 return Results.Ok(new ApiResponse<ReferenceResponse>(true,new(booking.Reference)));
}).RequireRateLimiting("forms");

app.MapPost("/api/auth/register",async(RegisterRequest input,CeyvoriaDb db,IPasswordHasher<AppUser> hasher,CancellationToken ct)=>{
 var bad=Validate(input);if(bad is not null)return bad;
 var email=input.Email.Trim().ToLowerInvariant();
 if(await db.Users.AnyAsync(u=>u.Email==email,ct))return Results.Conflict(new{success=false,message="This email address is already registered."});
 var u=new AppUser{FirstName=input.FirstName.Trim(),LastName=input.LastName.Trim(),Email=email,Role="Customer"};
 u.PasswordHash=hasher.HashPassword(u,input.Password);db.Users.Add(u);await db.SaveChangesAsync(ct);
 return Results.Ok(new ApiResponse<object>(true,new{message="Account created. You can now sign in."}));
}).RequireRateLimiting("auth");
app.MapPost("/api/auth/login",async(LoginRequest input,CeyvoriaDb db,IPasswordHasher<AppUser> hasher,IConfiguration config,CancellationToken ct)=>{
 var u=await db.Users.FirstOrDefaultAsync(x=>x.Email==input.Email.Trim().ToLowerInvariant()&&x.Active,ct);
 if(u is null||hasher.VerifyHashedPassword(u,u.PasswordHash,input.Password)==PasswordVerificationResult.Failed)return Results.Json(new{success=false,message="Invalid email or password."},statusCode:401);
 u.LastLoginAt=DateTimeOffset.UtcNow;await db.SaveChangesAsync(ct);
 return Ok(new AuthResponse(Token(u,config),u.Role,$"{u.FirstName} {u.LastName}".Trim()));
}).RequireRateLimiting("auth");
app.MapGet("/api/auth/me",async(ClaimsPrincipal principal,CeyvoriaDb db,CancellationToken ct)=>{
 var id=principal.FindFirstValue(ClaimTypes.NameIdentifier);
 if(!Guid.TryParse(id,out var uid))return Results.Unauthorized();
 var u=await db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==uid&&x.Active,ct);
 return u is null?Results.Unauthorized():Ok(new{u.Id,u.FirstName,u.LastName,u.Email,u.Role});
}).RequireAuthorization();
app.MapGet("/api/account/bookings",async(ClaimsPrincipal p,CeyvoriaDb db,CancellationToken ct)=>{
 var id=Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
 var user=await db.Users.FindAsync([id],ct);
 if(user is null)return Results.Unauthorized();
 var b=await db.Bookings.AsNoTracking().Where(x=>x.UserId==id).OrderByDescending(x=>x.CreatedAt).Select(x=>new{x.Reference,x.StartDate,x.Travellers,x.Status,x.PaymentStatus,x.CreatedAt}).ToListAsync(ct);
 return Ok(b);
}).RequireAuthorization();

// Intentionally fail-closed: no paid status is assumed from a browser return URL.
// Implement provider-hosted session creation and verified webhooks before enabling live payments.
app.MapPost("/api/payments/create",()=>Results.Json(new{success=false,message="Secure online payments are not enabled until merchant approval and verified webhooks are configured."},statusCode:503));
app.MapPost("/api/payments/webhook",()=>Results.StatusCode(501));

// Admin-only endpoints (backend authorization, not just hidden UI).
var admin=app.MapGroup("/api/admin").RequireAuthorization(p=>p.RequireRole("Admin"));
admin.MapGet("/dashboard",async(CeyvoriaDb db,CancellationToken ct)=>Ok(new{
 bookings=await db.Bookings.CountAsync(ct),pending=await db.Bookings.CountAsync(x=>x.Status=="PendingEnquiry",ct),
 confirmed=await db.Bookings.CountAsync(x=>x.Status=="Confirmed",ct),
 tours=await db.Tours.CountAsync(ct),destinations=await db.Destinations.CountAsync(ct),
 enquiries=await db.CustomTrips.CountAsync(ct),messages=await db.ContactMessages.CountAsync(ct),
 // Revenue is not estimated or fabricated. Only verified paid payment records would count.
 revenue=await db.Payments.Where(x=>x.Status=="Paid"&&x.VerifiedAt!=null).SumAsync(x=>(decimal?)x.Amount,ct)??0m
}));
admin.MapGet("/bookings",async(CeyvoriaDb db,CancellationToken ct)=>Ok(await db.Bookings.AsNoTracking().OrderByDescending(x=>x.CreatedAt).Take(100).ToListAsync(ct)));
admin.MapGet("/custom-trips",async(CeyvoriaDb db,CancellationToken ct)=>Ok(await db.CustomTrips.AsNoTracking().OrderByDescending(x=>x.CreatedAt).Take(100).ToListAsync(ct)));
admin.MapGet("/messages",async(CeyvoriaDb db,CancellationToken ct)=>Ok(await db.ContactMessages.AsNoTracking().OrderByDescending(x=>x.CreatedAt).Take(100).ToListAsync(ct)));
admin.MapGet("/tours",async(CeyvoriaDb db,CancellationToken ct)=>Ok(await db.Tours.AsNoTracking().OrderBy(x=>x.Title).ToListAsync(ct)));
admin.MapGet("/destinations",async(CeyvoriaDb db,CancellationToken ct)=>Ok(await db.Destinations.AsNoTracking().OrderBy(x=>x.Name).ToListAsync(ct)));
admin.MapGet("/experiences",async(CeyvoriaDb db,CancellationToken ct)=>Ok(await db.Experiences.AsNoTracking().ToListAsync(ct)));
admin.MapGet("/travel-styles",async(CeyvoriaDb db,CancellationToken ct)=>Ok(await db.TravelStyles.AsNoTracking().ToListAsync(ct)));
admin.MapGet("/blog",async(CeyvoriaDb db,CancellationToken ct)=>Ok(await db.BlogPosts.AsNoTracking().ToListAsync(ct)));
admin.MapGet("/faqs",async(CeyvoriaDb db,CancellationToken ct)=>Ok(await db.Faqs.AsNoTracking().ToListAsync(ct)));
admin.MapGet("/customers",async(CeyvoriaDb db,CancellationToken ct)=>Ok(await db.Users.AsNoTracking().Select(x=>new{x.Id,x.FirstName,x.LastName,x.Email,x.Role,x.Active,x.CreatedAt}).ToListAsync(ct)));
admin.MapGet("/payments",async(CeyvoriaDb db,CancellationToken ct)=>Ok(await db.Payments.AsNoTracking().ToListAsync(ct)));
admin.MapPost("/tours",async(Tour input,CeyvoriaDb db,CancellationToken ct)=>{
 input.Id=Guid.NewGuid();input.CreatedAt=DateTimeOffset.UtcNow;input.UpdatedAt=input.CreatedAt;
 if(string.IsNullOrWhiteSpace(input.Title)||string.IsNullOrWhiteSpace(input.Slug))return Results.BadRequest(new{success=false,message="Title and slug are required."});
 if(await db.Tours.AnyAsync(x=>x.Slug==input.Slug,ct))return Results.Conflict(new{success=false,message="A tour with that slug already exists."});
 db.Tours.Add(input);await db.SaveChangesAsync(ct);return Ok(new{input.Id,input.Slug});
});
admin.MapPut("/tours/{id:guid}",async(Guid id,Tour input,CeyvoriaDb db,CancellationToken ct)=>{
 var x=await db.Tours.FindAsync([id],ct);if(x is null)return Results.NotFound();
 x.Title=input.Title;x.Slug=input.Slug;x.Description=input.Description;x.Days=input.Days;x.Nights=input.Nights;x.Style=input.Style;x.Region=input.Region;x.Image=input.Image;x.RouteJson=input.RouteJson;x.ItineraryJson=input.ItineraryJson;x.StartingPrice=input.StartingPrice;x.Currency=input.Currency;x.Published=input.Published;
 await db.SaveChangesAsync(ct);return Ok(new{x.Id});
});
admin.MapDelete("/tours/{id:guid}",async(Guid id,CeyvoriaDb db,CancellationToken ct)=>{
 var x=await db.Tours.FindAsync([id],ct);if(x is null)return Results.NotFound();
 x.Published=false;await db.SaveChangesAsync(ct);return Ok(new{x.Id});
});
admin.MapPut("/bookings/{id:guid}/status",async(Guid id,BookingStatusRequest input,CeyvoriaDb db,CancellationToken ct)=>{
 var x=await db.Bookings.FindAsync([id],ct);if(x is null)return Results.NotFound();
 var allowed=new[]{"PendingEnquiry","ProposalSent","Confirmed","Cancelled","Completed"};
 if(!allowed.Contains(input.Status))return Results.BadRequest(new{success=false,message="Invalid status"});
 x.Status=input.Status;x.AdminNotes=input.AdminNotes;await db.SaveChangesAsync(ct);return Ok(new{x.Id,x.Status});
});


// Content CRUD: write operations remain protected by the Admin policy.
AdminCrudHelpers.MapCrud<Destination>(admin,"destinations");
AdminCrudHelpers.MapCrud<Experience>(admin,"experiences");
AdminCrudHelpers.MapCrud<TravelStyle>(admin,"travel-styles");
AdminCrudHelpers.MapCrud<BlogPost>(admin,"blog");
AdminCrudHelpers.MapCrud<Faq>(admin,"faqs");
admin.MapPut("/custom-trips/{id:guid}/status",async(Guid id,UpdateStatus input,CeyvoriaDb db,CancellationToken ct)=>{
 var x=await db.CustomTrips.FindAsync([id],ct);if(x is null)return Results.NotFound();
 if(!new[]{"New","Contacted","Planning","ProposalSent","Confirmed","Rejected","Completed"}.Contains(input.Status))return Results.BadRequest(new{success=false,message="Invalid status."});
 x.Status=input.Status;x.AdminNotes=input.Notes;await db.SaveChangesAsync(ct);return Ok(new{x.Id,x.Status});
});
admin.MapPut("/messages/{id:guid}/status",async(Guid id,UpdateStatus input,CeyvoriaDb db,CancellationToken ct)=>{
 var x=await db.ContactMessages.FindAsync([id],ct);if(x is null)return Results.NotFound();
 if(!new[]{"New","Read","Replied","Closed"}.Contains(input.Status))return Results.BadRequest(new{success=false,message="Invalid status."});
 x.Status=input.Status;await db.SaveChangesAsync(ct);return Ok(new{x.Id,x.Status});
});

var autoInit = app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("AUTO_INIT_DB") || !string.IsNullOrWhiteSpace(builder.Configuration["ADMIN_BOOTSTRAP_PASSWORD"]);
if (autoInit) {
 using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<CeyvoriaDb>();
 await db.Database.EnsureCreatedAsync();await SeedData.SeedAsync(db);
 var adminEmail=builder.Configuration["ADMIN_BOOTSTRAP_EMAIL"];
 var adminPass=builder.Configuration["ADMIN_BOOTSTRAP_PASSWORD"];
 if(!string.IsNullOrWhiteSpace(adminEmail)&&!string.IsNullOrWhiteSpace(adminPass)&&adminPass.Length>=12&&!await db.Users.AnyAsync(u=>u.Role=="Admin")){
  var u=new AppUser{Email=adminEmail.Trim().ToLowerInvariant(),FirstName="Admin",Role="Admin"};
  u.PasswordHash=scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>().HashPassword(u,adminPass);
  db.Users.Add(u);await db.SaveChangesAsync();
  app.Logger.LogWarning("Administrator account bootstrapped successfully.");
 }
}
app.Run();

static IResult Ok<T>(T data)=>Results.Ok(new ApiResponse<T>(true,data));
static object ToTour(Tour t)=>new{t.Title,t.Slug,t.Description,days=t.Days,nights=t.Nights,t.Style,t.Region,t.Image,t.StartingPrice,t.Currency,route=JsonSerializer.Deserialize<object>(t.RouteJson),itinerary=JsonSerializer.Deserialize<object>(t.ItineraryJson)};
static object ToDestination(Destination d)=>new{d.Name,d.Slug,d.DisplayName,d.Region,d.Location,d.Description,d.Image,d.Notes,activities=JsonSerializer.Deserialize<object>(d.ActivitiesJson)};
static string Reference(string prefix)=>$"CV-{prefix}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";
static IResult? Validate(object input){
 var errs=new List<ValidationResult>();
 return Validator.TryValidateObject(input,new ValidationContext(input),errs,true)?null:Results.ValidationProblem(errs.SelectMany(x=>x.MemberNames.DefaultIfEmpty("general").Select(n=>(name:n,msg:x.ErrorMessage??"Invalid input"))).GroupBy(x=>x.name).ToDictionary(x=>x.Key,x=>x.Select(v=>v.msg).ToArray()));
}
static string Token(AppUser u,IConfiguration config){
 var secret = config["Jwt:Secret"] ?? config["Jwt_Secret"] ?? config["JWT_SECRET"]!;
 var key=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
 var credentials=new SigningCredentials(key,SecurityAlgorithms.HmacSha256);
 var claims=new[]{new Claim(ClaimTypes.NameIdentifier,u.Id.ToString()),new Claim(ClaimTypes.Email,u.Email),new Claim(ClaimTypes.Role,u.Role)};
 var issuer = config["Jwt:Issuer"] ?? config["Jwt_Issuer"] ?? "CeyvoriaTours";
 var audience = config["Jwt:Audience"] ?? config["Jwt_Audience"] ?? "CeyvoriaToursWebsite";
 var jwt=new JwtSecurityToken(issuer:issuer,audience:audience,claims:claims,expires:DateTime.UtcNow.AddMinutes(20),signingCredentials:credentials);
 return new JwtSecurityTokenHandler().WriteToken(jwt);
}
public record BookingStatusRequest(string Status,string? AdminNotes);

public record UpdateStatus(string Status,string? Notes);
static class AdminCrudHelpers{
 public static void MapCrud<T>(RouteGroupBuilder group,string resource)where T:Entity{
  group.MapPost($"/{resource}",async(T input,CeyvoriaDb db,CancellationToken ct)=>{
   input.Id=Guid.NewGuid();input.CreatedAt=DateTimeOffset.UtcNow;input.UpdatedAt=input.CreatedAt;
   db.Set<T>().Add(input);await db.SaveChangesAsync(ct);return Results.Ok(new ApiResponse<object>(true,new{input.Id}));
  });
  group.MapPut($"/{resource}/{{id:guid}}",async(Guid id,T input,CeyvoriaDb db,CancellationToken ct)=>{
   var entity=await db.Set<T>().FindAsync([id],ct);if(entity is null)return Results.NotFound();
   input.Id=id;input.CreatedAt=entity.CreatedAt;
   db.Entry(entity).CurrentValues.SetValues(input);await db.SaveChangesAsync(ct);
   return Results.Ok(new ApiResponse<object>(true,new{entity.Id}));
  });
  group.MapDelete($"/{resource}/{{id:guid}}",async(Guid id,CeyvoriaDb db,CancellationToken ct)=>{
   var entity=await db.Set<T>().FindAsync([id],ct);if(entity is null)return Results.NotFound();
   var p=typeof(T).GetProperty("Published");
   if(p is not null&&p.PropertyType==typeof(bool))p.SetValue(entity,false);
   else db.Set<T>().Remove(entity);
   await db.SaveChangesAsync(ct);return Results.Ok(new ApiResponse<object>(true,new{entity.Id}));
  });
 }
}
