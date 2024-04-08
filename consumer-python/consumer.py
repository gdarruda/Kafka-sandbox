from confluent_kafka import Consumer
import boto3
import json
from datetime import datetime
from multiprocessing import Pool

BATCH_SIZE = 25

conf = {'bootstrap.servers': 'localhost:9093',
        'group.id': 'python-consumer',
        'auto.offset.reset': 'smallest',
        'enable.auto.commit': "false",
        'max.poll.interval.ms': '3600000'}

client = boto3.client('dynamodb',
                      endpoint_url='http://localhost:8000',
                      region_name='sa-east-1')

consumer = Consumer(conf)
running = True


def format_items(value: str) -> dict:
    
    prediction = json.loads(value)

    client_id = {
        'ClientId': {'S': prediction['ClientId']}
    }

    classes = dict([(k,{'S': str(v)}) for k,v in prediction['Classes'].items()])

    return {'PutRequest': {'Item': {**client_id, **classes}}}


def save_dynamo(predictions: list[dict]) -> dict:

    result = client.batch_write_item(
        RequestItems={'Prediction': predictions}
    )

    return result


if __name__ == "__main__":

    try:
                            
        consumer.subscribe(["predictions"])
        batch = []
        count = 0
        start = datetime.now()

        while running:
            
            msg = consumer.poll(timeout=1.0)
            execute_batch = False

            if msg is None: 
                execute_batch = True
            else:
                batch.append(msg.value())
            
            if len(batch) >= 100_000:
                execute_batch = True

            if not execute_batch: continue
            if len(batch) == 0: continue

            print(f"Começando ({len(batch)}): {datetime.now().isoformat()}")

            with Pool(processes=12) as pool:
                predictions = pool.map(format_items, batch)
            
            predictions = list(predictions)
            num_predictions = len(predictions)

            if num_predictions <= BATCH_SIZE:
                chuncked = [predictions]
            else:
                chuncked = [
                    predictions[i*BATCH_SIZE:min((i*BATCH_SIZE)+BATCH_SIZE,num_predictions)]
                    for i 
                    in range(num_predictions // BATCH_SIZE)
                ]
            
            with Pool(processes=48) as pool:
                pool.map(save_dynamo, chuncked)

            count += num_predictions
            print(f"Total: {count} Tempo: {datetime.now() - start}")

            batch=[]

    finally:
        consumer.close()
