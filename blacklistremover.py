import json
import time

def remove_strings_from_blacklist():
    # Load the JSON array from the file
    with open("blacklist.json", "r") as file:
        blacklist = json.load(file)

    # Remove strings from the top down
    if blacklist:
        removed_string = blacklist.pop(0)
        print(f"Removed string: {removed_string}")

    # Save the updated JSON array back to the file
    with open("blacklist.json", "w") as file:
        json.dump(blacklist, file)

while True:
    remove_strings_from_blacklist()
    time.sleep(20)  # Sleep for 5 minutes (300 seconds)
