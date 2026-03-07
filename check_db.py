import subprocess, os

psql = r'C:\Program Files\PostgreSQL\18\bin\psql.exe'
env = dict(os.environ)
env['PGPASSWORD'] = 'Admin@123@123!'

def q(sql):
    r = subprocess.run([psql, '-h', 'localhost', '-U', 'postgres', '-d', 'InfluencerMatchDb', '-c', sql],
                       capture_output=True, text=True, env=env)
    return r.stdout or r.stderr

print("=== USERS ===")
print(q('SELECT "Id", "Name", "Role", "Email" FROM "Users";'))

print("=== CreatorProfiles ===")
print(q('SELECT "CreatorProfileId", "UserId", "Language", "Category", "Country" FROM "CreatorProfiles";'))

print("=== CreatorChannels ===")
print(q('SELECT "ChannelId", "CreatorProfileId", "ChannelName", "Subscribers" FROM "CreatorChannels";'))

print("=== Influencers ===")
print(q('SELECT "InfluencerId", "UserId", "YouTubeLink", "Followers", "Category" FROM "Influencers";'))
