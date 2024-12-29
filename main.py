from pyspark.sql import SparkSession
from pyspark.sql.functions import col, size, split, length, lit, trim
import requests
import pandas as pd

class ELTProcessor:
    def __init__(self, app_name: str, access_key: str, secret_key: str, endpoint: str):
        """
        Initialize the ELTProcessor class.
        :param app_name: Name of the Spark application.
        :param access_key: AWS S3 Access Key.
        :param secret_key: AWS S3 Secret Key.
        :param endpoint: S3 endpoint URL.
        """
        self.spark = SparkSession.builder \
            .appName(app_name) \
            .config("spark.hadoop.fs.s3a.access.key", access_key) \
            .config("spark.hadoop.fs.s3a.secret.key", secret_key) \
            .config("spark.hadoop.fs.s3a.endpoint", endpoint) \
            .getOrCreate()

    def fetch_data(self, url: str):
        """
        Fetch data from the provided URL.
        :param url: The URL to fetch data from.
        :return: List of JSON data.
        """
        response = requests.get(url)
        if response.status_code == 200:
            return response.json()
        else:
            print(f"Error fetching data: {response.status_code}")
            return []

    def transform_data(self, data: list):
        """
        Transform the raw JSON data into a Spark DataFrame with multiple transformations.
        :param data: Raw JSON data.
        :return: Transformed Spark DataFrame.
        """
        # Convert the fetched data to a Spark DataFrame
        df = pd.DataFrame(data)
        spark_df = self.spark.createDataFrame(df)

        # Apply multiple transformations

        # Step 1: Filter posts with userId == 1
        spark_df_transformed = spark_df.filter(col("userId") == 1) \
                                        .select("id", "title", "body")

        # Step 2: Add word count column
        spark_df_transformed = spark_df_transformed.withColumn("body_word_count", size(split(col("body"), r'\s+')))

        # Step 3: Add post length column (number of characters in body)
        spark_df_transformed = spark_df_transformed.withColumn("post_length", length(col("body")))

        # Step 4: Add a new column to check if 'lorem' is in the body text
        spark_df_transformed = spark_df_transformed.withColumn("contains_lorem", col("body").rlike("lorem"))

        # Step 5: Rename columns for better readability
        spark_df_transformed = spark_df_transformed.withColumnRenamed("id", "post_id") \
                                                   .withColumnRenamed("title", "post_title") \
                                                   .withColumnRenamed("body", "post_body")

        # Step 6: Trim whitespaces from the 'title'
        spark_df_transformed = spark_df_transformed.withColumn("post_title", trim(col("post_title")))

        # Step 7: Fill missing values in 'title' and 'body' with default text
        spark_df_transformed = spark_df_transformed.fillna({"post_body": "No content available", "post_title": "Untitled"})

        return spark_df_transformed

    def write_data(self, df, output_path: str):
        """
        Write the transformed DataFrame to the output path (local or S3).
        :param df: The transformed Spark DataFrame.
        :param output_path: The output path (local or S3).
        """
        try:
            # Write to the specified path in overwrite mode
            df.write.mode("overwrite").json(output_path)
            print(f"Data successfully saved to {output_path}")
        except Exception as e:
            print(f"Error writing data to JSON: {e}")
            print(f"Exception Type: {type(e)}")
            print(f"Exception Details: {e}")

def main():
    # Configuration values
    app_name = "ELT-Upload"
    access_key = "AKIAWNHTHMZCH6MZW6"
    secret_key = "HX7ocpfzdcjLMpYrEzi+WMuhB6o7hRpkXlOk"
    endpoint = "s3.ap-southeast-2.amazon\aws.com"
    url = "https://jsonplaceholder.typicode.com/posts"
    bucket_name = "pysparrk-test"
    output_path = f"s3a://{bucket_name}/transformed_data/output.json"

    # Create an instance of the ELTProcessor class
    processor = ELTProcessor(app_name, access_key, secret_key, endpoint)

    # Fetch the data from the API
    data = processor.fetch_data(url)

    # If data is available, proceed with transformation and saving
    if data:
        # Transform the data
        transformed_df = processor.transform_data(data)

        # Write the transformed data to the specified path
        processor.write_data(transformed_df, output_path)

if __name__ == "__main__":
    main()
