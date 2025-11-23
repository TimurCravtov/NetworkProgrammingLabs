import requests
import time
import matplotlib.pyplot as plt
import subprocess
import os
import random
from concurrent.futures import ThreadPoolExecutor

# Configuration
LEADER_URL = "http://localhost:8080"
FOLLOWER_URLS = [f"http://localhost:{8081+i}" for i in range(5)]
NUM_WRITES = 1000
NUM_KEYS = 10
CONCURRENCY = 20  # >10 threads

def run_command(cmd, env=None, cwd=None):
    subprocess.run(cmd, shell=True, check=True, env=env, cwd=cwd)

def write_task(key, value):
    start = time.time()
    try:
        resp = requests.post(f"{LEADER_URL}/put", json={"key": key, "value": value}, timeout=10)
        latency = time.time() - start
        return resp.status_code == 200, latency
    except Exception as e:
        # print(f"Write error: {e}")
        return False, time.time() - start

def check_consistency(keys):
    mismatches = 0
    print("Verifying consistency...")
    
    # Get Leader Data
    leader_data = {}
    for key in keys:
        try:
            resp = requests.get(f"{LEADER_URL}/get/{key}", timeout=2)
            if resp.status_code == 200:
                leader_data[key] = resp.json()
            else:
                leader_data[key] = None
        except:
            leader_data[key] = None

    # Check Followers
    for f_idx, f_url in enumerate(FOLLOWER_URLS):
        for key in keys:
            try:
                resp = requests.get(f"{f_url}/get/{key}", timeout=2)
                follower_val = resp.json() if resp.status_code == 200 else None
            except:
                follower_val = None
            
            if leader_data.get(key) != follower_val:
                mismatches += 1
                # print(f"Mismatch Key={key}: Leader={leader_data.get(key)} Follower{f_idx+1}={follower_val}")
    
    return mismatches

def check_consistency_getall():
    mismatches = 0
    print("Verifying consistency (using /getall)...")
    
    # Get Leader Data
    leader_data = {}
    try:
        resp = requests.get(f"{LEADER_URL}/getall", timeout=5)
        if resp.status_code == 200:
            leader_data = resp.json()
        else:
            print(f"Failed to get all data from Leader: {resp.status_code}")
            return -1 # Error
    except Exception as e:
        print(f"Exception getting data from Leader: {e}")
        return -1

    # Check Followers
    for f_idx, f_url in enumerate(FOLLOWER_URLS):
        follower_data = {}
        try:
            resp = requests.get(f"{f_url}/getall", timeout=5)
            if resp.status_code == 200:
                follower_data = resp.json()
            else:
                print(f"Failed to get all data from Follower {f_idx+1}: {resp.status_code}")
                mismatches += 1
                continue
        except Exception as e:
            print(f"Exception getting data from Follower {f_idx+1}: {e}")
            mismatches += 1
            continue
            
        # Compare dictionaries
        # We only care if keys present in leader match in follower. 
        # Or should we check exact match? 
        # Given the experiment, exact match is expected for the keys we wrote.
        # But let's just check if leader_data == follower_data
        
        if leader_data != follower_data:
            mismatches += 1
            # Find differences for logging (optional)
            # diff_keys = set(leader_data.keys()) ^ set(follower_data.keys())
            # if diff_keys:
            #     print(f"Key set mismatch with Follower {f_idx+1}: {diff_keys}")
            # else:
            #     for k in leader_data:
            #         if leader_data[k] != follower_data.get(k):
            #             print(f"Value mismatch for key {k}: Leader={leader_data[k]}, Follower={follower_data.get(k)}")

    return mismatches

def run_experiment():
    # Move to parent directory where docker-compose.yml is located
    project_root = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
    
    quorums = [1, 2, 3, 4, 5]
    avg_latencies = []
    
    for q in quorums:
        print(f"\n=== Testing Write Quorum: {q} ===")
        env = os.environ.copy()
        env["WRITE_QUORUM"] = str(q)
        
        print("Restarting cluster...")
        try:
            # Down first to clear memory
            run_command("docker compose down", env=env, cwd=project_root)
            run_command("docker compose up -d", env=env, cwd=project_root)
        except subprocess.CalledProcessError as e:
            print(f"Docker command failed: {e}")
            return

        print("Waiting for leader to be ready...")
        time.sleep(15) # Wait for dotnet to start
        
        keys = [f"key-{i}" for i in range(NUM_KEYS)]
        latencies = []
        
        print(f"Performing {NUM_WRITES} writes with {CONCURRENCY} threads...")
        
        with ThreadPoolExecutor(max_workers=CONCURRENCY) as executor:
            futures = []
            for i in range(NUM_WRITES):
                key = random.choice(keys)
                val = f"value-{i}"
                futures.append(executor.submit(write_task, key, val))
            
            completed = 0
            for f in futures:
                success, lat = f.result()
                if success:
                    latencies.append(lat)
                completed += 1
                if completed % 1000 == 0:
                    print(f"Completed {completed}/{NUM_WRITES} requests")
        
        if latencies:
            avg = sum(latencies) / len(latencies)
            avg_latencies.append(avg)
            print(f"Average Latency: {avg:.4f}s")
        else:
            avg_latencies.append(0)
            print("All writes failed.")

        mismatches = check_consistency_getall()
        print(f"Consistency check: {mismatches} mismatches found.")
        
        if mismatches == 0:
            print("Result: Strong consistency observed (Data matches).")
        else:
            print("Result: Inconsistency observed.")

    # Plotting
    plt.figure(figsize=(10, 6))
    plt.plot(quorums, avg_latencies, marker='o', linestyle='-', color='b')
    plt.title('Write Quorum vs Average Write Latency')
    plt.xlabel('Write Quorum (Number of Followers)')
    plt.ylabel('Average Latency (seconds)')
    plt.grid(True)
    output_file = os.path.join(os.path.dirname(__file__), 'latency_plot.png')
    plt.savefig(output_file)
    print(f"\nPlot saved to {output_file}")
    
    print("\nExplanation:")
    print("1. Latency vs Quorum: As the write quorum increases, the leader must wait for acknowledgments from more followers.")
    print("   This introduces additional network latency and processing time (waiting for the slowest of the N required followers).")
    print("   Therefore, we expect the average latency to increase with the quorum size.")
    print("2. Consistency: In this implementation, followers update their state immediately upon receiving a request.")
    print("   The leader updates its state only after the quorum is reached.")
    print("   Since we are checking consistency after all writes have completed and the system is quiescent,")
    print("   we expect the data to be consistent across all nodes, assuming no permanent failures occurred.")
    print("   If a write failed (quorum not reached), the leader wouldn't have updated, but some followers might have.")
    print("   This could lead to inconsistency if we checked failed writes, but for successful writes, all should match.")

if __name__ == "__main__":
    run_experiment()

