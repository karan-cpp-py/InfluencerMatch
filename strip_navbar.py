import os

base = r"e:\InfluencerAI\frontend\influencer-match\src\views"
views = [
    "HomeView","LoginView","RegisterView","InfluencerDashboard","BrandDashboard",
    "BrandCampaigns","BrandAnalytics","CampaignList","CreatorAnalyticsView",
    "ScoreLeaderboard","CreatorSearch","CreatorDiscovery","CreatorCompare",
    "InfluencerList","InfluencerDetail","ResultsView"
]

for v in views:
    path = os.path.join(base, v + ".vue")
    with open(path, "r", encoding="utf-8") as f:
        content = f.read()
    before = content
    content = content.replace("    <Navbar />\n", "")
    content = content.replace("\nimport Navbar from '../components/Navbar.vue';", "")
    # Also remove component registration if Options API style
    content = content.replace(',\n  components: { Navbar }', '')
    content = content.replace('\n  components: { Navbar }', '')
    with open(path, "w", encoding="utf-8") as f:
        f.write(content)
    status = "CHANGED" if content != before else "unchanged"
    left = "<Navbar />" in content or "import Navbar" in content
    print(f"{v}: {status}, residual={left}")
