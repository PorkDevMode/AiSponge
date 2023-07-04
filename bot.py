import pytchat
import json
import time
import os

# Constants
TOPICS_FILE = 'topics.json'
BLACKLIST_FILE = r"blacklist.json"
SUBMISSION_LIMIT = 1
SUBMISSION_INTERVAL = 10  # Cooldown for suggesting topics (KEEP LOW)
TOPIC_EXPIRY = 1200  # How long it takes for a topic to expire in seconds
BYPASS_USERS = ['AI_SouthPark.', 'SPCookie']  # List of users who can bypass the time limit

# Global variables
user_submissions = {}
topic_timestamps = {}


# Function to process chat messages
def process_chat_messages(chat):
    topics = []

    # Remove expired topics
    current_time = time.time()
    expired_topics = []
    for topic, timestamp in topic_timestamps.items():
        if current_time - timestamp > TOPIC_EXPIRY:
            expired_topics.append(topic)
    for topic in expired_topics:
        del topic_timestamps[topic]

    # Load the blacklist from the blacklist.json file
    blacklist = []
    if os.path.exists(BLACKLIST_FILE):
        with open(BLACKLIST_FILE, 'r') as file:
            blacklist = json.load(file)

    for c in chat.get().sync_items():
        if c.message.startswith('!topic'):
            user_id = c.author.channelId

            # Check if the user can bypass the time limit
            if user_id not in BYPASS_USERS:
                # Check if the user has reached the submission limit
                if user_id in user_submissions and current_time - user_submissions[user_id] < SUBMISSION_INTERVAL:
                    continue

            # Extract the topic from the message
            topic = c.message[7:].strip()

            # Check if the topic is in the blacklist
            if topic in blacklist:
                continue

            # Add the topic to the list if it is not empty
            if topic:
                topics.append(topic)
                topic_timestamps[topic] = current_time

            # Update the submission timestamp for the user
            user_submissions[user_id] = current_time

    # Read existing topics from the topics.json file
    try:
        with open(TOPICS_FILE, 'r') as file:
            existing_topics = json.load(file)
    except FileNotFoundError:
        existing_topics = []

    # Remove expired topics from existing topics
    existing_topics = [topic for topic in existing_topics if topic not in expired_topics]

    # Remove blacklisted topics from existing topics
    existing_topics = [topic for topic in existing_topics if topic not in blacklist]

    # Combine existing topics and new topics
    combined_topics = existing_topics + topics

    # Convert topics list to JSON string
    json_topics = json.dumps(combined_topics)

    # Write topics to topics.json file
    with open(TOPICS_FILE, 'w') as file:
        file.write(json_topics)

    # Print the JSON string
    print(json_topics)

# Set up pytchat
chat = pytchat.create(video_id='DPbdWsYWlRo')

# Continuously process chat messages
while chat.is_alive():
    process_chat_messages(chat)
    time.sleep(1)  # Add a delay to avoid excessive API requests

# Cleanup
chat.terminate()
