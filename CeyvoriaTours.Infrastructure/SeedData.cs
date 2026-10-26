using System.Text.Json;
using CeyvoriaTours.Domain;
using Microsoft.EntityFrameworkCore;
namespace CeyvoriaTours.Infrastructure;
public static class SeedData {
 static string FilePath(string name)=>Path.Combine(AppContext.BaseDirectory,"seed",name+".json");
 static IEnumerable<JsonElement> Rows(string name){
  var path=FilePath(name);if(!File.Exists(path))return [];
  using var doc=JsonDocument.Parse(File.ReadAllText(path));
  return doc.RootElement.EnumerateArray().Select(e=>e.Clone()).ToList();
 }
 static string Text(JsonElement e,string name)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(name,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()??"":"";
 static string Raw(JsonElement e,string name)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(name,out var v)?v.GetRawText():"[]";
 public static async Task SeedAsync(CeyvoriaDb db,CancellationToken ct=default){
  if(!await db.Destinations.AnyAsync(ct)){foreach(var r in Rows("destinations"))db.Destinations.Add(new Destination{Name=Text(r,"name"),Slug=Text(r,"slug"),DisplayName=Text(r,"displayName"),Region=Text(r,"region"),Location=Text(r,"location"),Description=Text(r,"description"),Image=Text(r,"image"),ActivitiesJson=Raw(r,"activities"),Notes=Text(r,"notes")});}
  if(!await db.Tours.AnyAsync(ct)){foreach(var r in Rows("tours"))db.Tours.Add(new Tour{Title=Text(r,"title"),Slug=Text(r,"slug"),Description=Text(r,"description"),Days=r.ValueKind==JsonValueKind.Object&&r.TryGetProperty("days",out var d)?d.GetInt32():0,Nights=r.ValueKind==JsonValueKind.Object&&r.TryGetProperty("nights",out var n)?n.GetInt32():0,Style=Text(r,"style"),Region=Text(r,"region"),Image=Text(r,"image"),RouteJson=Raw(r,"route"),ItineraryJson=Raw(r,"itinerary"),StartingPrice=null});}
  if(!await db.Experiences.AnyAsync(ct)){foreach(var r in Rows("experiences"))db.Experiences.Add(new Experience{Name=Text(r,"name"),Slug=Text(r,"slug"),Description=Text(r,"description"),Category=Text(r,"category"),Image=Text(r,"image")});}
  if(!await db.TravelStyles.AnyAsync(ct)){foreach(var r in Rows("styles"))db.TravelStyles.Add(new TravelStyle{Name=Text(r,"name"),Slug=Text(r,"slug"),Description=Text(r,"description"),Image=Text(r,"image")});}
  if(!await db.BlogPosts.AnyAsync(ct)){foreach(var r in Rows("articles"))db.BlogPosts.Add(new BlogPost{Title=Text(r,"title"),Slug=Text(r,"slug"),Excerpt=Text(r,"excerpt"),Category=Text(r,"category"),Image=Text(r,"image"),Content=Text(r,"content")});}
  if(!await db.Faqs.AnyAsync(ct)){
   foreach(var r in Rows("faqs")){
    string q = "", a = "", c = "";
    if(r.ValueKind == JsonValueKind.Array && r.GetArrayLength() >= 2){
     q = r[0].GetString() ?? "";
     a = r[1].GetString() ?? "";
     c = r.GetArrayLength() > 2 ? r[2].GetString() ?? "" : "";
    } else if(r.ValueKind == JsonValueKind.Object){
     q = Text(r, "question");
     a = Text(r, "answer");
     c = Text(r, "category");
    }
    db.Faqs.Add(new Faq{Question=q,Answer=a,Category=c});
   }
  }
  await db.SaveChangesAsync(ct);
 }
}

