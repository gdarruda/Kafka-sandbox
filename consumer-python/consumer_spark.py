from pyspark.sql import SparkSession

spark = SparkSession.builder.appName("SimpleApp").getOrCreate()

df = (spark
      .readStream
      .format("kafka")
      .option("kafka.bootstrap.servers", "localhost:9093")
      .option("subscribe", "predictions")
      .option("startingOffsets", "earliest")
      .load())

content = df.selectExpr("CAST(value AS STRING)")

(content
 .writeStream
 .format("console")
 .outputMode("append")
 .start()
 .awaitTermination())