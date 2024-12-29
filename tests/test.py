import unittest
from unittest.mock import patch, MagicMock
from pyspark.sql import SparkSession
import requests
import pandas as pd
from Mock_api_elt import ELTProcessor 

class TestELTProcessor(unittest.TestCase):

    def setUp(self):
        """Set up the test environment."""
        self.app_name = "ELT-Upload"
        self.access_key = "AKIAWNHTHMZCH6MZWS"
        self.secret_key = "HX7ocpfzdcjLMpYrEzi+VBWMuhB6o7hRpkXlOk"
        self.endpoint = "s3.ap-southeast-2.amazonaws.com"
        self.processor = ELTProcessor(self.app_name, self.access_key, self.secret_key, self.endpoint)
        self.url = "https://jsonplaceholder.typicode.com/posts"
        
        # Mock the Spark session for testing
        self.spark = SparkSession.builder.appName(self.app_name).getOrCreate()

    @patch('requests.get')
    def test_fetch_data_success(self, mock_get):
        """Test if fetch_data method correctly fetches data."""
        mock_response = MagicMock()
        mock_response.status_code = 200
        mock_response.json.return_value = [{"userId": 1, "id": 1, "title": "Test", "body": "Test body"}]
        mock_get.return_value = mock_response

        data = self.processor.fetch_data(self.url)

        # Assert that the fetched data matches expected output
        self.assertEqual(len(data), 1)
        self.assertEqual(data[0]['title'], "Test")

    @patch('requests.get')
    def test_fetch_data_failure(self, mock_get):
        """Test if fetch_data handles errors when the API request fails."""
        mock_response = MagicMock()
        mock_response.status_code = 404
        mock_get.return_value = mock_response

        data = self.processor.fetch_data(self.url)

        # Assert that an empty list is returned on failure
        self.assertEqual(data, [])

    def test_transform_data(self):
        """Test if the transform_data method applies transformations correctly."""
        data = [{"userId": 1, "id": 1, "title": "Test Post", "body": "Lorem ipsum dolor sit amet."}]
        
        # Transform data
        transformed_df = self.processor.transform_data(data)

        # Convert to Pandas DataFrame for easy assertions
        pandas_df = transformed_df.toPandas()

        # Test transformations
        self.assertEqual(pandas_df['post_id'][0], 1)
        self.assertEqual(pandas_df['post_title'][0], "Test Post")
        self.assertEqual(pandas_df['body_word_count'][0], 5)  # "Lorem ipsum dolor sit amet" has 5 words
        self.assertEqual(pandas_df['post_length'][0], len("Lorem ipsum dolor sit amet."))  # Length of the body
        self.assertTrue(pandas_df['contains_lorem'][0])  # Contains "lorem"

    @patch('pyspark.sql.DataFrame.write')
    def test_write_data_success(self, mock_write):
        """Test if write_data handles writing correctly."""
        # Mock the DataFrame's write method
        mock_write.return_value = None  # Simulating a successful write
        
        # Create a dummy DataFrame
        data = [{"userId": 1, "id": 1, "title": "Test Post", "body": "Test body"}]
        transformed_df = self.processor.transform_data(data)

        output_path = "s3a://test-bucket/output.json"
        self.processor.write_data(transformed_df, output_path)

        # Assert that the write method was called once
        mock_write.assert_called_once()

    @patch('pyspark.sql.DataFrame.write')
    def test_write_data_failure(self, mock_write):
        """Test if write_data handles errors when writing fails."""
        # Simulating a write error
        mock_write.side_effect = Exception("Error writing to S3")

        # Create a dummy DataFrame
        data = [{"userId": 1, "id": 1, "title": "Test Post", "body": "Test body"}]
        transformed_df = self.processor.transform_data(data)

        output_path = "s3a://test-bucket/output.json"
        
        with self.assertRaises(Exception):
            self.processor.write_data(transformed_df, output_path)

    def tearDown(self):
        """Clean up after each test."""
        self.spark.stop()

if __name__ == "__main__":
    unittest.main()
